using System.Buffers;
using LisperFlow.Streaming;
using LisperFlow.Streaming.Models;
using Microsoft.Extensions.Logging;

namespace LisperFlow.AudioCapture;

/// <summary>
/// Orchestrates audio capture, VAD, and buffering
/// </summary>
public class AudioCaptureService : IAudioStreamProvider, IDisposable
{
    private readonly WasapiCaptureManager _captureManager;
    private readonly VoiceActivityDetector _vad;
    private readonly RingBufferManager _ringBuffer;
    private readonly AudioConfiguration _config;
    private readonly ILogger<AudioCaptureService> _logger;
    
    private bool _isCapturing;
    private bool _isSpeechActive;
    private DateTime _speechStartTime;
    private DateTime _lastSpeechTime;
    private readonly float[] _resampleBuffer;
    private bool _disposed;
    private bool _streamingMode;
    private readonly List<float> _streamingBuffer = new();
    
    // Speech segment collection (VAD-based)
    private readonly List<float> _currentSpeechSegment = new();
    
    // Push-to-talk buffer (all audio during recording)
    private readonly List<float> _pttBuffer = new();
    
    /// <summary>
    /// Event raised when a complete speech segment is ready for processing
    /// </summary>
    public event EventHandler<SpeechSegmentEventArgs>? SpeechSegmentReady;
    
    /// <summary>
    /// Event raised when speech is detected (VAD triggered)
    /// </summary>
    public event EventHandler? SpeechStarted;
    
    /// <summary>
    /// Event raised when speech ends (silence detected)
    /// </summary>
    public event EventHandler? SpeechEnded;
    
    public event EventHandler<AudioChunkEventArgs>? AudioChunkReady;
    
    /// <summary>
    /// Current recording state
    /// </summary>
    public RecordingState State => _captureManager.State;
    
    /// <summary>
    /// Whether speech is currently being detected
    /// </summary>
    public bool IsSpeechActive => _isSpeechActive;
    
    public AudioCaptureService(
        AudioConfiguration config,
        ILogger<AudioCaptureService> logger,
        ILogger<WasapiCaptureManager> captureLogger,
        ILogger<VoiceActivityDetector> vadLogger)
    {
        _config = config;
        _logger = logger;
        
        _captureManager = new WasapiCaptureManager(config, captureLogger);
        _vad = new VoiceActivityDetector(config.VadThreshold, config.SampleRate, vadLogger);
        _ringBuffer = new RingBufferManager(config.BufferSeconds, config.SampleRate);
        
        // Buffer for resampling (max expected is 48000 Hz stereo to 16000 Hz mono)
        _resampleBuffer = new float[48000 * 2]; // 1 second at max rate
        
        _captureManager.AudioDataAvailable += OnAudioDataAvailable;
        _captureManager.StateChanged += OnStateChanged;
    }
    
    /// <summary>
    /// Get available audio devices
    /// </summary>
    public List<AudioDevice> GetDevices() => _captureManager.EnumerateDevices();
    
    /// <summary>
    /// Initialize the audio capture system
    /// </summary>
    public async Task<bool> InitializeAsync(string? deviceId = null, string? vadModelPath = null)
    {
        // Initialize VAD if model path provided
        if (!string.IsNullOrEmpty(vadModelPath))
        {
            bool vadOk = await _vad.InitializeAsync(vadModelPath);
            if (!vadOk)
            {
                _logger.LogWarning("VAD model not loaded, using energy-based detection");
            }
        }
        else
        {
            _logger.LogInformation("No VAD model path specified, using energy-based detection");
        }
        
        // Initialize audio capture
        return await _captureManager.InitializeAsync(deviceId);
    }
    
    /// <summary>
    /// Start listening for audio (continuous capture)
    /// </summary>
    public async Task StartListeningAsync()
    {
        _vad.Reset();
        _ringBuffer.Clear();
        _currentSpeechSegment.Clear();
        _pttBuffer.Clear();  // Clear PTT buffer too
        _streamingBuffer.Clear();
        _isCapturing = true;
        _isSpeechActive = false;
        
        await _captureManager.StartCaptureAsync();
        _logger.LogInformation("Audio capture service started listening");
    }
    
    /// <summary>
    /// Stop listening and return any pending speech
    /// </summary>
    public async Task<float[]?> StopListeningAsync()
    {
        _isCapturing = false;
        await _captureManager.StopCaptureAsync();
        
        // For push-to-talk: return all accumulated audio
        if (_pttBuffer.Count > 0)
        {
            var segment = _pttBuffer.ToArray();
            _pttBuffer.Clear();
            _currentSpeechSegment.Clear();
            _isSpeechActive = false;
            
            _logger.LogInformation(
                "Returning PTT audio: {Duration:F2}s, {Samples} samples",
                segment.Length / (float)_config.SampleRate,
                segment.Length);
            
            return segment;
        }
        
        // Fallback: return active speech segment if any
        if (_currentSpeechSegment.Count > 0)
        {
            var segment = _currentSpeechSegment.ToArray();
            _currentSpeechSegment.Clear();
            _isSpeechActive = false;
            
            _logger.LogInformation(
                "Returning speech segment: {Duration:F2}s",
                segment.Length / (float)_config.SampleRate);
            
            return segment;
        }
        
        _logger.LogWarning("No audio captured during recording");
        return null;
    }
    
    public async Task StartStreamingAsync(CancellationToken cancellationToken = default)
    {
        if (_isCapturing)
        {
            _logger.LogWarning("Audio capture already active; reusing for streaming");
        }
        
        _streamingMode = true;
        _streamingBuffer.Clear();
        _vad.Reset();
        
        if (!_isCapturing)
        {
            await StartListeningAsync();
        }
        
        _logger.LogInformation(
            "Streaming audio capture started (chunk: {ChunkMs}ms)",
            _config.StreamingChunkDurationMs);
    }
    
    public async Task StopStreamingAsync(CancellationToken cancellationToken = default)
    {
        _streamingMode = false;
        
        // Emit any remaining samples as a final chunk
        EmitStreamingChunk(final: true);
        
        if (_isCapturing)
        {
            await StopListeningAsync();
        }
        
        _logger.LogInformation("Streaming audio capture stopped");
    }
    
    /// <summary>
    /// Get the current buffered audio (for push-to-talk mode)
    /// </summary>
    public float[] GetBufferedAudio()
    {
        if (_currentSpeechSegment.Count > 0)
        {
            return _currentSpeechSegment.ToArray();
        }
        
        return _ringBuffer.ReadAll();
    }
    
    private void OnAudioDataAvailable(object? sender, AudioDataAvailableEventArgs e)
    {
        if (!_isCapturing || _disposed) return;
        
        try
        {
            // Convert bytes to float samples
            var samples = ConvertToFloat(e.Buffer, e.BytesRecorded);
            NormalizeAudio(samples);
            
            // Add to ring buffer (for pre-roll)
            _ringBuffer.Write(samples);
            
            // Accumulate PTT buffer only in non-streaming mode
            if (!_streamingMode)
            {
                _pttBuffer.AddRange(samples);
            }
            
            // Check VAD on chunks (for continuous mode / events)
            ProcessVad(samples.AsSpan(), allowSegmentCompletion: !_streamingMode);
            
            // If speech is active, also accumulate to VAD segment
            if (_isSpeechActive && !_streamingMode)
            {
                _currentSpeechSegment.AddRange(samples);
            }
            
            // Emit streaming chunks when enabled
            if (_streamingMode)
            {
                _streamingBuffer.AddRange(samples);
                EmitStreamingChunk();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing audio data");
        }
    }
    
    private void ProcessVad(ReadOnlySpan<float> samples, bool allowSegmentCompletion)
    {
        // Process in 512-sample chunks (Silero VAD optimal)
        const int chunkSize = 512;
        bool speechInChunk = false;
        
        for (int i = 0; i < samples.Length; i += chunkSize)
        {
            int remaining = Math.Min(chunkSize, samples.Length - i);
            var chunk = samples.Slice(i, remaining);
            
            if (_vad.IsSpeech(chunk))
            {
                speechInChunk = true;
                _lastSpeechTime = DateTime.UtcNow;
            }
        }
        
        // State transitions
        if (speechInChunk && !_isSpeechActive)
        {
            // Speech started
            _isSpeechActive = true;
            _speechStartTime = DateTime.UtcNow;
            
            // Include pre-roll audio
            var preRoll = _ringBuffer.GetPreRoll(_config.PreRollSamples);
            _currentSpeechSegment.Clear();
            _currentSpeechSegment.AddRange(preRoll);
            
            _logger.LogDebug("Speech detected, starting segment with {PreRoll} pre-roll samples", preRoll.Length);
            SpeechStarted?.Invoke(this, EventArgs.Empty);
        }
        else if (!speechInChunk && _isSpeechActive)
        {
            // Check if silence has been long enough to end speech
            var silenceDuration = DateTime.UtcNow - _lastSpeechTime;
            if (silenceDuration.TotalMilliseconds > 500) // 500ms silence = end of speech
            {
                _isSpeechActive = false;
                
                if (allowSegmentCompletion && _currentSpeechSegment.Count > 0)
                {
                    var segment = _currentSpeechSegment.ToArray();
                    _currentSpeechSegment.Clear();
                    
                    float durationSeconds = segment.Length / (float)_config.SampleRate;
                    _logger.LogDebug("Speech segment complete: {Duration:F2}s, {Samples} samples", 
                        durationSeconds, segment.Length);
                    
                    // Only emit if segment is long enough (> 0.3 seconds)
                    if (durationSeconds > 0.3f)
                    {
                        SpeechSegmentReady?.Invoke(this, new SpeechSegmentEventArgs
                        {
                            Samples = segment,
                            SampleRate = _config.SampleRate,
                            Duration = TimeSpan.FromSeconds(durationSeconds)
                        });
                    }
                }
                else
                {
                    _currentSpeechSegment.Clear();
                }
                
                SpeechEnded?.Invoke(this, EventArgs.Empty);
            }
        }
    }
    
    private void EmitStreamingChunk(bool final = false)
    {
        int chunkSamples = _config.StreamingChunkSamples;
        if (chunkSamples <= 0) return;
        
        while (_streamingBuffer.Count >= chunkSamples || (final && _streamingBuffer.Count > 0))
        {
            int count = final ? _streamingBuffer.Count : chunkSamples;
            var chunkSamplesArray = _streamingBuffer.GetRange(0, count).ToArray();
            _streamingBuffer.RemoveRange(0, count);
            
            AudioChunkReady?.Invoke(this, new AudioChunkEventArgs
            {
                Chunk = new AudioChunk
                {
                    Samples = chunkSamplesArray,
                    SampleRate = _config.SampleRate,
                    Timestamp = DateTime.UtcNow
                }
            });
            
            if (final) break;
        }
    }
    
    private float[] ConvertToFloat(byte[] buffer, int bytesRecorded)
    {
        // Handle 32-bit IEEE float stereo (common WASAPI format)
        // Each sample is 4 bytes, 2 channels = 8 bytes per stereo sample
        int bytesPerStereoSample = 8; // 4 bytes * 2 channels
        int stereoSampleCount = bytesRecorded / bytesPerStereoSample;
        var monoSamples = new float[stereoSampleCount];
        
        for (int i = 0; i < stereoSampleCount; i++)
        {
            // Read left and right channels as float
            float left = BitConverter.ToSingle(buffer, i * bytesPerStereoSample);
            float right = BitConverter.ToSingle(buffer, i * bytesPerStereoSample + 4);
            
            // Mix to mono
            monoSamples[i] = (left + right) / 2.0f;
        }
        
        // Resample from 48kHz to 16kHz (3:1 ratio) if needed
        if (_captureManager.WaveFormat?.SampleRate == 48000 && _config.SampleRate == 16000)
        {
            return ResampleSimple(monoSamples, 48000, 16000);
        }
        
        return monoSamples;
    }
    
    private static float[] ResampleSimple(float[] samples, int fromRate, int toRate)
    {
        // Simple linear interpolation resampling
        double ratio = (double)fromRate / toRate;
        int outputLength = (int)(samples.Length / ratio);
        var output = new float[outputLength];
        
        for (int i = 0; i < outputLength; i++)
        {
            double srcIndex = i * ratio;
            int srcIndexInt = (int)srcIndex;
            double frac = srcIndex - srcIndexInt;
            
            if (srcIndexInt + 1 < samples.Length)
            {
                output[i] = (float)(samples[srcIndexInt] * (1 - frac) + samples[srcIndexInt + 1] * frac);
            }
            else
            {
                output[i] = samples[srcIndexInt];
            }
        }
        
        return output;
    }

    private static void NormalizeAudio(float[] samples)
    {
        float peak = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float abs = Math.Abs(samples[i]);
            if (abs > peak) peak = abs;
        }

        if (peak < 0.01f) return;

        float targetPeak = 0.707f; // -3 dB
        float gain = targetPeak / peak;
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] *= gain;
        }
    }
    
    private void OnStateChanged(object? sender, RecordingStateChangedEventArgs e)
    {
        _logger.LogDebug("Recording state changed to: {State}", e.State);
        
        if (e.State == RecordingState.Error && !string.IsNullOrEmpty(e.ErrorMessage))
        {
            _logger.LogError("Recording error: {Error}", e.ErrorMessage);
        }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _captureManager.AudioDataAvailable -= OnAudioDataAvailable;
        _captureManager.StateChanged -= OnStateChanged;
        
        _captureManager.Dispose();
        _vad.Dispose();
        
        _logger.LogDebug("AudioCaptureService disposed");
    }
}

/// <summary>
/// Event args for completed speech segments
/// </summary>
public class SpeechSegmentEventArgs : EventArgs
{
    public float[] Samples { get; set; } = Array.Empty<float>();
    public int SampleRate { get; set; }
    public TimeSpan Duration { get; set; }
}

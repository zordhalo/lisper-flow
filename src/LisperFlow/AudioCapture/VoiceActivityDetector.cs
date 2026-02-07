using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace LisperFlow.AudioCapture;

/// <summary>
/// Voice Activity Detection using Silero VAD ONNX model
/// </summary>
public class VoiceActivityDetector : IDisposable
{
    private InferenceSession? _session;
    private readonly float _threshold;
    private readonly int _sampleRate;
    private readonly ILogger<VoiceActivityDetector> _logger;
    private bool _initialized;
    private bool _disposed;
    
    // Silero VAD state tensors
    private float[] _h = new float[2 * 1 * 64];
    private float[] _c = new float[2 * 1 * 64];
    
    /// <summary>
    /// Whether the VAD model is loaded and ready
    /// </summary>
    public bool IsInitialized => _initialized;
    
    public VoiceActivityDetector(
        float threshold = 0.5f, 
        int sampleRate = 16000,
        ILogger<VoiceActivityDetector>? logger = null)
    {
        _threshold = threshold;
        _sampleRate = sampleRate;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<VoiceActivityDetector>.Instance;
    }
    
    /// <summary>
    /// Initialize the VAD model from file
    /// </summary>
    public async Task<bool> InitializeAsync(string modelPath)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!File.Exists(modelPath))
                {
                    _logger.LogError("VAD model file not found: {Path}", modelPath);
                    return false;
                }
                
                var sessionOptions = new SessionOptions
                {
                    ExecutionMode = ExecutionMode.ORT_PARALLEL,
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
                };
                
                _session = new InferenceSession(modelPath, sessionOptions);
                _initialized = true;
                
                _logger.LogInformation("VAD model loaded from {Path}", modelPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load VAD model");
                return false;
            }
        });
    }
    
    /// <summary>
    /// Detect if audio samples contain speech
    /// </summary>
    /// <param name="samples">Audio samples (float, normalized -1.0 to 1.0)</param>
    /// <returns>Speech probability (0.0 to 1.0)</returns>
    public float GetSpeechProbability(ReadOnlySpan<float> samples)
    {
        if (!_initialized || _session == null)
        {
            // Fall back to energy-based detection
            return GetEnergyBasedProbability(samples);
        }
        
        try
        {
            // Silero VAD expects chunks of 512 samples at 16kHz
            var audioChunk = samples.ToArray();
            
            // Create input tensors
            var inputDims = new int[] { 1, audioChunk.Length };
            var inputTensor = new DenseTensor<float>(audioChunk, inputDims);
            var srTensor = new DenseTensor<long>(new long[] { _sampleRate }, new int[] { 1 });
            var hTensor = new DenseTensor<float>(_h, new int[] { 2, 1, 64 });
            var cTensor = new DenseTensor<float>(_c, new int[] { 2, 1, 64 });
            
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", inputTensor),
                NamedOnnxValue.CreateFromTensor("sr", srTensor),
                NamedOnnxValue.CreateFromTensor("h", hTensor),
                NamedOnnxValue.CreateFromTensor("c", cTensor)
            };
            
            using var results = _session.Run(inputs);
            
            // Get output probability
            var output = results.FirstOrDefault(r => r.Name == "output");
            if (output != null)
            {
                var outputTensor = output.AsTensor<float>();
                float probability = outputTensor.GetValue(0);
                
                // Update hidden states for next call
                var hnOutput = results.FirstOrDefault(r => r.Name == "hn");
                var cnOutput = results.FirstOrDefault(r => r.Name == "cn");
                
                if (hnOutput != null)
                {
                    hnOutput.AsTensor<float>().ToArray().CopyTo(_h, 0);
                }
                if (cnOutput != null)
                {
                    cnOutput.AsTensor<float>().ToArray().CopyTo(_c, 0);
                }
                
                return probability;
            }
            
            return 0f;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "VAD inference failed, falling back to energy-based detection");
            return GetEnergyBasedProbability(samples);
        }
    }
    
    /// <summary>
    /// Check if samples contain speech (binary decision)
    /// </summary>
    public bool IsSpeech(ReadOnlySpan<float> samples)
    {
        return GetSpeechProbability(samples) >= _threshold;
    }
    
    /// <summary>
    /// Energy-based fallback VAD (simple RMS threshold)
    /// </summary>
    public float GetEnergyBasedProbability(ReadOnlySpan<float> samples)
    {
        if (samples.Length == 0) return 0f;
        
        double energy = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            energy += samples[i] * samples[i];
        }
        
        double rms = Math.Sqrt(energy / samples.Length);
        
        // Map RMS to probability (tuned thresholds)
        // RMS < 0.01 = probably silence
        // RMS > 0.05 = probably speech
        float probability = (float)Math.Clamp((rms - 0.01) / 0.04, 0.0, 1.0);
        
        return probability;
    }
    
    /// <summary>
    /// Reset the internal state (call when starting new recording)
    /// </summary>
    public void Reset()
    {
        Array.Clear(_h);
        Array.Clear(_c);
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _session?.Dispose();
        _session = null;
        _initialized = false;
        
        _logger.LogDebug("VoiceActivityDetector disposed");
    }
}

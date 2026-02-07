using NAudio.CoreAudioApi;
using NAudio.Wave;
using Microsoft.Extensions.Logging;

namespace LisperFlow.AudioCapture;

/// <summary>
/// Manages WASAPI audio capture from the microphone
/// </summary>
public class WasapiCaptureManager : IDisposable
{
    private WasapiCapture? _capture;
    private MMDevice? _device;
    private readonly AudioConfiguration _config;
    private readonly ILogger<WasapiCaptureManager> _logger;
    private RecordingState _state = RecordingState.Idle;
    private bool _disposed;
    
    /// <summary>
    /// Event raised when audio data is available
    /// </summary>
    public event EventHandler<AudioDataAvailableEventArgs>? AudioDataAvailable;
    
    /// <summary>
    /// Event raised when recording state changes
    /// </summary>
    public event EventHandler<RecordingStateChangedEventArgs>? StateChanged;
    
    /// <summary>
    /// Current recording state
    /// </summary>
    public RecordingState State => _state;
    
    /// <summary>
    /// Current audio format
    /// </summary>
    public WaveFormat? WaveFormat => _capture?.WaveFormat;
    
    /// <summary>
    /// Current device name
    /// </summary>
    public string? DeviceName => _device?.FriendlyName;
    
    public WasapiCaptureManager(AudioConfiguration config, ILogger<WasapiCaptureManager> logger)
    {
        _config = config;
        _logger = logger;
    }
    
    /// <summary>
    /// Enumerate available audio capture devices
    /// </summary>
    public List<AudioDevice> EnumerateDevices()
    {
        var devices = new List<AudioDevice>();
        
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            
            // Get default device
            MMDevice? defaultDevice = null;
            try
            {
                defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get default audio device");
            }
            
            // Enumerate all active capture devices
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                devices.Add(new AudioDevice
                {
                    Id = device.ID,
                    FriendlyName = device.FriendlyName,
                    IsDefault = defaultDevice != null && device.ID == defaultDevice.ID
                });
            }
            
            _logger.LogInformation("Found {Count} audio capture devices", devices.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate audio devices");
        }
        
        return devices;
    }
    
    /// <summary>
    /// Initialize audio capture with specified device
    /// </summary>
    public async Task<bool> InitializeAsync(string? deviceId = null)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(WasapiCaptureManager));
        
        return await Task.Run(() =>
        {
            try
            {
                SetState(RecordingState.Starting);
                
                using var enumerator = new MMDeviceEnumerator();
                
                // Get device
                if (string.IsNullOrEmpty(deviceId))
                {
                    _device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                    _logger.LogInformation("Using default audio device: {DeviceName}", _device.FriendlyName);
                }
                else
                {
                    _device = enumerator.GetDevice(deviceId);
                    _logger.LogInformation("Using specified audio device: {DeviceName}", _device.FriendlyName);
                }
                
                // Create WASAPI capture in shared mode for compatibility
                _capture = new WasapiCapture(_device, true, _config.LatencyMs);
                
                // NAudio will use device's native format
                // We'll need to resample if needed
                _logger.LogInformation(
                    "Audio format: {Format} (Target: {SampleRate}Hz, {BitsPerSample}bit, {Channels}ch)",
                    _capture.WaveFormat,
                    _config.SampleRate,
                    _config.BitsPerSample,
                    _config.Channels);
                
                // Hook events
                _capture.DataAvailable += OnDataAvailable;
                _capture.RecordingStopped += OnRecordingStopped;
                
                SetState(RecordingState.Stopped);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize audio capture");
                SetState(RecordingState.Error, ex.Message);
                return false;
            }
        });
    }
    
    /// <summary>
    /// Start audio capture
    /// </summary>
    public Task StartCaptureAsync()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(WasapiCaptureManager));
        if (_capture == null) throw new InvalidOperationException("Audio capture not initialized. Call InitializeAsync first.");
        
        return Task.Run(() =>
        {
            try
            {
                SetState(RecordingState.Starting);
                _capture.StartRecording();
                SetState(RecordingState.Recording);
                _logger.LogInformation("Audio capture started");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start audio capture");
                SetState(RecordingState.Error, ex.Message);
                throw;
            }
        });
    }
    
    /// <summary>
    /// Stop audio capture
    /// </summary>
    public Task StopCaptureAsync()
    {
        if (_disposed) return Task.CompletedTask;
        if (_capture == null) return Task.CompletedTask;
        
        return Task.Run(() =>
        {
            try
            {
                SetState(RecordingState.Stopping);
                _capture.StopRecording();
                // State will be set to Stopped in OnRecordingStopped
                _logger.LogInformation("Audio capture stopping");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop audio capture");
                SetState(RecordingState.Error, ex.Message);
            }
        });
    }
    
    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0) return;
        
        try
        {
            // Copy buffer immediately to avoid blocking audio thread
            var buffer = new byte[e.BytesRecorded];
            Buffer.BlockCopy(e.Buffer, 0, buffer, 0, e.BytesRecorded);
            
            // Raise event on ThreadPool to not block audio callback
            ThreadPool.QueueUserWorkItem(_ =>
            {
                AudioDataAvailable?.Invoke(this, new AudioDataAvailableEventArgs
                {
                    Buffer = buffer,
                    BytesRecorded = buffer.Length,
                    Timestamp = DateTime.UtcNow
                });
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing audio data");
        }
    }
    
    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            _logger.LogError(e.Exception, "Recording stopped with error");
            SetState(RecordingState.Error, e.Exception.Message);
        }
        else
        {
            SetState(RecordingState.Stopped);
            _logger.LogInformation("Audio capture stopped");
        }
    }
    
    private void SetState(RecordingState state, string? errorMessage = null)
    {
        _state = state;
        StateChanged?.Invoke(this, new RecordingStateChangedEventArgs
        {
            State = state,
            ErrorMessage = errorMessage
        });
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        if (_capture != null)
        {
            _capture.DataAvailable -= OnDataAvailable;
            _capture.RecordingStopped -= OnRecordingStopped;
            
            if (_state == RecordingState.Recording)
            {
                _capture.StopRecording();
            }
            
            _capture.Dispose();
            _capture = null;
        }
        
        _device?.Dispose();
        _device = null;
        
        _logger.LogDebug("WasapiCaptureManager disposed");
    }
}

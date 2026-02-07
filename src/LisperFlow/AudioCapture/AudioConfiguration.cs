namespace LisperFlow.AudioCapture;

/// <summary>
/// Configuration for audio capture
/// </summary>
public class AudioConfiguration
{
    /// <summary>
    /// Sample rate in Hz (default: 16000 for Whisper)
    /// </summary>
    public int SampleRate { get; set; } = 16000;
    
    /// <summary>
    /// Bits per sample (default: 16)
    /// </summary>
    public int BitsPerSample { get; set; } = 16;
    
    /// <summary>
    /// Number of audio channels (default: 1 = mono)
    /// </summary>
    public int Channels { get; set; } = 1;
    
    /// <summary>
    /// WASAPI buffer latency in milliseconds
    /// </summary>
    public int LatencyMs { get; set; } = 50;
    
    /// <summary>
    /// Device ID for capture (null = default device)
    /// </summary>
    public string? DeviceId { get; set; }
    
    /// <summary>
    /// Voice Activity Detection threshold (0.0 to 1.0)
    /// </summary>
    public float VadThreshold { get; set; } = 0.5f;
    
    /// <summary>
    /// Ring buffer capacity in seconds
    /// </summary>
    public int BufferSeconds { get; set; } = 10;
    
    /// <summary>
    /// Pre-roll milliseconds to capture before VAD triggers
    /// </summary>
    public int PreRollMs { get; set; } = 300;
    
    /// <summary>
    /// Streaming audio chunk duration in milliseconds
    /// </summary>
    public int StreamingChunkDurationMs { get; set; } = 100;
    
    /// <summary>
    /// Gets the number of bytes per sample
    /// </summary>
    public int BytesPerSample => BitsPerSample / 8;
    
    /// <summary>
    /// Gets the bytes per second
    /// </summary>
    public int BytesPerSecond => SampleRate * Channels * BytesPerSample;
    
    /// <summary>
    /// Gets the number of samples for pre-roll
    /// </summary>
    public int PreRollSamples => (SampleRate * PreRollMs) / 1000;
    
    /// <summary>
    /// Gets the number of samples per streaming chunk
    /// </summary>
    public int StreamingChunkSamples => (SampleRate * StreamingChunkDurationMs) / 1000;
}

/// <summary>
/// Represents an audio capture device
/// </summary>
public class AudioDevice
{
    public string Id { get; set; } = "";
    public string FriendlyName { get; set; } = "";
    public bool IsDefault { get; set; }
    
    public override string ToString() => FriendlyName;
}

/// <summary>
/// Event args for audio data available
/// </summary>
public class AudioDataAvailableEventArgs : EventArgs
{
    /// <summary>
    /// Raw audio buffer (byte array from capture)
    /// </summary>
    public byte[] Buffer { get; set; } = Array.Empty<byte>();
    
    /// <summary>
    /// Number of bytes recorded in this callback
    /// </summary>
    public int BytesRecorded { get; set; }
    
    /// <summary>
    /// Timestamp when audio was captured
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Event args for recording state changes
/// </summary>
public class RecordingStateChangedEventArgs : EventArgs
{
    public RecordingState State { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Recording state enumeration
/// </summary>
public enum RecordingState
{
    Idle,
    Starting,
    Recording,
    Stopping,
    Stopped,
    Error
}

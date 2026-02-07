namespace LisperFlow.ASR;

/// <summary>
/// Interface for ASR (Automatic Speech Recognition) providers
/// </summary>
public interface IAsrProvider
{
    /// <summary>
    /// Provider name for logging/display
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Whether this provider is available (model loaded, API key valid, etc.)
    /// </summary>
    bool IsAvailable { get; }
    
    /// <summary>
    /// Transcribe audio samples to text
    /// </summary>
    /// <param name="audioSamples">Float audio samples, normalized -1.0 to 1.0</param>
    /// <param name="sampleRate">Sample rate of the audio (typically 16000)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ASR result with transcript</returns>
    Task<AsrResult> TranscribeAsync(
        float[] audioSamples, 
        int sampleRate = 16000,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from ASR transcription
/// </summary>
public class AsrResult
{
    /// <summary>
    /// Transcribed text
    /// </summary>
    public string Transcript { get; set; } = "";
    
    /// <summary>
    /// Confidence score (0.0 to 1.0), if available
    /// </summary>
    public float Confidence { get; set; } = 1.0f;
    
    /// <summary>
    /// Processing latency in milliseconds
    /// </summary>
    public long LatencyMs { get; set; }
    
    /// <summary>
    /// Provider that generated this result
    /// </summary>
    public string Provider { get; set; } = "";
    
    /// <summary>
    /// Additional metadata (language detected, etc.)
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
    
    /// <summary>
    /// Whether the transcription was successful
    /// </summary>
    public bool Success { get; set; } = true;
    
    /// <summary>
    /// Error message if transcription failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    public static AsrResult Empty => new() { Transcript = "" };
    
    public static AsrResult Failed(string errorMessage) => new() 
    { 
        Success = false, 
        ErrorMessage = errorMessage 
    };
}

/// <summary>
/// Exception for ASR-related errors
/// </summary>
public class AsrException : Exception
{
    public AsrException(string message) : base(message) { }
    public AsrException(string message, Exception innerException) : base(message, innerException) { }
}

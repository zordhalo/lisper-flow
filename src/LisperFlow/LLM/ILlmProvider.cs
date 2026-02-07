namespace LisperFlow.LLM;

/// <summary>
/// Interface for LLM (Large Language Model) providers
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// Provider name for logging/display
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Whether this provider is available
    /// </summary>
    bool IsAvailable { get; }
    
    /// <summary>
    /// Generate text based on system and user prompts
    /// </summary>
    Task<LlmResult> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from LLM generation
/// </summary>
public class LlmResult
{
    /// <summary>
    /// Generated text content
    /// </summary>
    public string GeneratedText { get; set; } = "";
    
    /// <summary>
    /// Number of tokens generated
    /// </summary>
    public int TokensGenerated { get; set; }
    
    /// <summary>
    /// Processing latency in milliseconds
    /// </summary>
    public long LatencyMs { get; set; }
    
    /// <summary>
    /// Provider that generated this result
    /// </summary>
    public string Provider { get; set; } = "";
    
    /// <summary>
    /// Whether generation was successful
    /// </summary>
    public bool Success { get; set; } = true;
    
    /// <summary>
    /// Error message if generation failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    public static LlmResult Failed(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}

/// <summary>
/// Exception for LLM-related errors
/// </summary>
public class LlmException : Exception
{
    public LlmException(string message) : base(message) { }
    public LlmException(string message, Exception innerException) : base(message, innerException) { }
}

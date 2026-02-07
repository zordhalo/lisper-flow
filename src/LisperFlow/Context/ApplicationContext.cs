namespace LisperFlow.Context;

/// <summary>
/// Tone preference for text enhancement
/// </summary>
public enum ToneType
{
    Default,
    Professional,
    Casual,
    Technical,
    Creative
}

/// <summary>
/// Context about the current application
/// </summary>
public class ApplicationContext
{
    public string ProcessName { get; set; } = "";
    public string WindowTitle { get; set; } = "";
    public string ControlType { get; set; } = "";
    public ToneType SuggestedTone { get; set; } = ToneType.Default;
    
    /// <summary>
    /// Infer tone from application name
    /// </summary>
    public static ToneType InferTone(string processName)
    {
        var lower = processName?.ToLowerInvariant() ?? "";
        
        return lower switch
        {
            "outlook" or "gmail" or "thunderbird" => ToneType.Professional,
            "slack" or "discord" or "teams" or "telegram" => ToneType.Casual,
            "code" or "vscode" or "cursor" or "devenv" or "visualstudio" => ToneType.Technical,
            "notion" or "obsidian" or "scrivener" => ToneType.Default,
            _ => ToneType.Default
        };
    }
}

namespace LisperFlow.TextInjection;

/// <summary>
/// Information about the currently focused UI element
/// </summary>
public class FocusedElementInfo
{
    public IntPtr WindowHandle { get; set; }
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = "";
    public string WindowTitle { get; set; } = "";
    public string ControlType { get; set; } = "";
    public bool IsTextInputCapable { get; set; }
    public System.Windows.Rect BoundingRectangle { get; set; }
}

/// <summary>
/// Result of text injection attempt
/// </summary>
public class InjectionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string StrategyUsed { get; set; } = "";
    public long LatencyMs { get; set; }
    
    public static InjectionResult Succeeded(string strategy, long latencyMs)
    {
        return new InjectionResult
        {
            Success = true,
            StrategyUsed = strategy,
            LatencyMs = latencyMs
        };
    }
    
    public static InjectionResult Failed(string errorMessage)
    {
        return new InjectionResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}

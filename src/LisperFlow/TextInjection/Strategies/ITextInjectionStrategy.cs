namespace LisperFlow.TextInjection.Strategies;

/// <summary>
/// Interface for text injection strategies
/// </summary>
public interface ITextInjectionStrategy
{
    /// <summary>
    /// Strategy name for logging/display
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Check if this strategy can handle the given element
    /// </summary>
    Task<bool> CanHandleAsync(FocusedElementInfo elementInfo, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Inject text into the element
    /// </summary>
    Task<InjectionResult> InjectAsync(string text, FocusedElementInfo elementInfo, CancellationToken cancellationToken = default);
}

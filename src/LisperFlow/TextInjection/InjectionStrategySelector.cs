using LisperFlow.TextInjection.Strategies;
using Microsoft.Extensions.Logging;

namespace LisperFlow.TextInjection;

/// <summary>
/// Selects and executes text injection strategies with fallback chain
/// </summary>
public class InjectionStrategySelector
{
    private readonly List<ITextInjectionStrategy> _strategies;
    private readonly FocusedElementDetector _elementDetector;
    private readonly ILogger<InjectionStrategySelector> _logger;
    
    public InjectionStrategySelector(
        FocusedElementDetector elementDetector,
        ILogger<InjectionStrategySelector> logger,
        ILogger<ClipboardPasteInjector> clipboardLogger,
        ILogger<SendInputInjector> sendInputLogger)
    {
        _elementDetector = elementDetector;
        _logger = logger;
        
        // Order matters: SendInput first to avoid clipboard-only behavior
        _strategies = new List<ITextInjectionStrategy>
        {
            new SendInputInjector(sendInputLogger),       // Direct typing
            new ClipboardPasteInjector(clipboardLogger)   // Fallback
        };
    }
    
    /// <summary>
    /// Inject text into the currently focused element
    /// </summary>
    public async Task<InjectionResult> InjectTextAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
        {
            return InjectionResult.Failed("Text is empty");
        }
        
        // Detect focused element
        var elementInfo = _elementDetector.GetFocusedElement();
        
        if (elementInfo == null)
        {
            return InjectionResult.Failed("No focused element detected");
        }
        
        if (!elementInfo.IsTextInputCapable)
        {
            _logger.LogWarning("Element may not be suitable for text input: {ControlType}", elementInfo.ControlType);
        }
        
        // Try each strategy in order
        foreach (var strategy in _strategies)
        {
            try
            {
                _logger.LogDebug("Trying strategy: {Strategy}", strategy.Name);
                
                if (await strategy.CanHandleAsync(elementInfo, cancellationToken))
                {
                    var result = await strategy.InjectAsync(text, elementInfo, cancellationToken);
                    
                    if (result.Success)
                    {
                        _logger.LogInformation(
                            "Text injected using {Strategy} in {Ms}ms to {Process}",
                            strategy.Name,
                            result.LatencyMs,
                            elementInfo.ProcessName);
                        
                        return result;
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Strategy {Strategy} failed: {Error}",
                            strategy.Name,
                            result.ErrorMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in strategy {Strategy}", strategy.Name);
            }
        }
        
        return InjectionResult.Failed("All injection strategies failed");
    }
    
    /// <summary>
    /// Inject text with specific strategy (for testing)
    /// </summary>
    public async Task<InjectionResult> InjectTextWithStrategyAsync(
        string text,
        string strategyName,
        CancellationToken cancellationToken = default)
    {
        var elementInfo = _elementDetector.GetFocusedElement();
        if (elementInfo == null)
        {
            return InjectionResult.Failed("No focused element detected");
        }
        
        var strategy = _strategies.FirstOrDefault(s => 
            s.Name.Equals(strategyName, StringComparison.OrdinalIgnoreCase));
        
        if (strategy == null)
        {
            return InjectionResult.Failed($"Strategy '{strategyName}' not found");
        }
        
        return await strategy.InjectAsync(text, elementInfo, cancellationToken);
    }
}

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace LisperFlow.TextInjection.Strategies;

/// <summary>
/// Text injection using keyboard input simulation.
/// Last resort strategy - types text character by character.
/// </summary>
public class SendInputInjector : ITextInjectionStrategy
{
    private readonly ILogger<SendInputInjector> _logger;
    
    public string Name => "SendInput";
    
    public SendInputInjector(ILogger<SendInputInjector> logger)
    {
        _logger = logger;
    }
    
    public Task<bool> CanHandleAsync(FocusedElementInfo elementInfo, CancellationToken cancellationToken = default)
    {
        // This works for everything as last resort
        return Task.FromResult(true);
    }
    
    public async Task<InjectionResult> InjectAsync(
        string text,
        FocusedElementInfo elementInfo,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Type text using SendInput with Unicode characters
            foreach (char c in text)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                    
                SendChar(c);
                
                // Small delay between characters to avoid dropped input
                await Task.Delay(5, cancellationToken);
            }
            
            stopwatch.Stop();
            
            _logger.LogDebug(
                "SendInput injection completed: {Length} chars in {Ms}ms",
                text.Length,
                stopwatch.ElapsedMilliseconds);
            
            return InjectionResult.Succeeded(Name, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendInput injection failed");
            return InjectionResult.Failed(ex.Message);
        }
    }
    
    private void SendChar(char c)
    {
        var inputs = new Hotkeys.Win32Interop.INPUT[2];
        
        // Key down (using Unicode)
        inputs[0] = new Hotkeys.Win32Interop.INPUT
        {
            type = Hotkeys.Win32Interop.INPUT_KEYBOARD,
            Input = new Hotkeys.Win32Interop.InputUnion
            {
                ki = new Hotkeys.Win32Interop.KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = Hotkeys.Win32Interop.KEYEVENTF_UNICODE
                }
            }
        };
        
        // Key up
        inputs[1] = new Hotkeys.Win32Interop.INPUT
        {
            type = Hotkeys.Win32Interop.INPUT_KEYBOARD,
            Input = new Hotkeys.Win32Interop.InputUnion
            {
                ki = new Hotkeys.Win32Interop.KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = Hotkeys.Win32Interop.KEYEVENTF_UNICODE | Hotkeys.Win32Interop.KEYEVENTF_KEYUP
                }
            }
        };
        
        Hotkeys.Win32Interop.SendInput(2, inputs, System.Runtime.InteropServices.Marshal.SizeOf<Hotkeys.Win32Interop.INPUT>());
    }
}

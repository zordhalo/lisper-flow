using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace LisperFlow.TextInjection.Strategies;

/// <summary>
/// Universal clipboard-based text injection using Ctrl+V
/// This is the most compatible strategy across all Windows applications.
/// </summary>
public class ClipboardPasteInjector : ITextInjectionStrategy
{
    private readonly ILogger<ClipboardPasteInjector> _logger;
    
    public string Name => "ClipboardPaste";
    
    public ClipboardPasteInjector(ILogger<ClipboardPasteInjector> logger)
    {
        _logger = logger;
    }
    
    public Task<bool> CanHandleAsync(FocusedElementInfo elementInfo, CancellationToken cancellationToken = default)
    {
        // This strategy works for any text input
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
            // Must run on STA thread for clipboard
            string? originalClipboard = null;
            
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    // Save original clipboard content
                    if (Clipboard.ContainsText())
                    {
                        originalClipboard = Clipboard.GetText();
                    }
                    
                    // Set our text to clipboard
                    Clipboard.SetText(text);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not save/set clipboard");
                    // Continue anyway, clipboard might be locked
                    try
                    {
                        Clipboard.SetText(text);
                    }
                    catch
                    {
                        // If we can't set clipboard at all, we fail
                        throw;
                    }
                }
            });
            
            // Wait for clipboard to update
            await Task.Delay(50, cancellationToken);
            
            // Simulate Ctrl+V
            SendCtrlV();
            
            // Wait a bit for paste to complete
            await Task.Delay(100, cancellationToken);
            
            // Restore original clipboard after a delay (fire and forget)
            _ = Task.Delay(500).ContinueWith(_ =>
            {
                Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        if (originalClipboard != null)
                        {
                            Clipboard.SetText(originalClipboard);
                        }
                        else
                        {
                            Clipboard.Clear();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not restore clipboard");
                    }
                });
            });
            
            stopwatch.Stop();
            
            _logger.LogDebug("Clipboard paste injection completed in {Ms}ms", stopwatch.ElapsedMilliseconds);
            
            return InjectionResult.Succeeded(Name, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Clipboard paste injection failed");
            return InjectionResult.Failed(ex.Message);
        }
    }
    
    private void SendCtrlV()
    {
        // Using SendInput for Ctrl+V
        var inputs = new Hotkeys.Win32Interop.INPUT[4];
        
        // Ctrl down
        inputs[0] = new Hotkeys.Win32Interop.INPUT
        {
            type = Hotkeys.Win32Interop.INPUT_KEYBOARD,
            Input = new Hotkeys.Win32Interop.InputUnion
            {
                ki = new Hotkeys.Win32Interop.KEYBDINPUT
                {
                    wVk = 0x11, // VK_CONTROL
                    dwFlags = 0
                }
            }
        };
        
        // V down
        inputs[1] = new Hotkeys.Win32Interop.INPUT
        {
            type = Hotkeys.Win32Interop.INPUT_KEYBOARD,
            Input = new Hotkeys.Win32Interop.InputUnion
            {
                ki = new Hotkeys.Win32Interop.KEYBDINPUT
                {
                    wVk = 0x56, // V
                    dwFlags = 0
                }
            }
        };
        
        // V up
        inputs[2] = new Hotkeys.Win32Interop.INPUT
        {
            type = Hotkeys.Win32Interop.INPUT_KEYBOARD,
            Input = new Hotkeys.Win32Interop.InputUnion
            {
                ki = new Hotkeys.Win32Interop.KEYBDINPUT
                {
                    wVk = 0x56,
                    dwFlags = Hotkeys.Win32Interop.KEYEVENTF_KEYUP
                }
            }
        };
        
        // Ctrl up
        inputs[3] = new Hotkeys.Win32Interop.INPUT
        {
            type = Hotkeys.Win32Interop.INPUT_KEYBOARD,
            Input = new Hotkeys.Win32Interop.InputUnion
            {
                ki = new Hotkeys.Win32Interop.KEYBDINPUT
                {
                    wVk = 0x11,
                    dwFlags = Hotkeys.Win32Interop.KEYEVENTF_KEYUP
                }
            }
        };
        
        Hotkeys.Win32Interop.SendInput(4, inputs, System.Runtime.InteropServices.Marshal.SizeOf<Hotkeys.Win32Interop.INPUT>());
    }
}

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
            _logger.LogInformation("Starting clipboard paste injection for {Length} characters", text.Length);
            
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
                        _logger.LogDebug("Saved original clipboard content");
                    }
                    
                    // Set our text to clipboard
                    Clipboard.SetText(text);
                    _logger.LogDebug("Set text to clipboard");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not save/set clipboard on first try");
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
            await Task.Delay(100, cancellationToken);
            
            // Verify clipboard was set correctly
            bool clipboardVerified = false;
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var clipboardText = Clipboard.GetText();
                    clipboardVerified = clipboardText == text;
                    _logger.LogDebug("Clipboard verification: {Result}", clipboardVerified);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not verify clipboard");
                }
            });
            
            if (!clipboardVerified)
            {
                _logger.LogWarning("Clipboard content does not match expected text");
            }
            
            // Simulate Ctrl+V
            _logger.LogDebug("Sending Ctrl+V key combination");
            bool sendSuccess = SendCtrlV();
            
            if (!sendSuccess)
            {
                _logger.LogError("SendInput failed to inject Ctrl+V");
                return InjectionResult.Failed("SendInput failed");
            }
            
            _logger.LogDebug("Ctrl+V sent successfully");
            
            // Wait a bit for paste to complete
            await Task.Delay(300, cancellationToken);
            
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
                            _logger.LogDebug("Restored original clipboard");
                        }
                        else
                        {
                            Clipboard.Clear();
                            _logger.LogDebug("Cleared clipboard");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not restore clipboard");
                    }
                });
            });
            
            stopwatch.Stop();
            
            _logger.LogInformation("Clipboard paste injection completed in {Ms}ms", stopwatch.ElapsedMilliseconds);
            
            return InjectionResult.Succeeded(Name, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Clipboard paste injection failed");
            return InjectionResult.Failed(ex.Message);
        }
    }
    
    private bool SendCtrlV()
    {
        try
        {
            // Create input array for Ctrl+V
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
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
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
                        wVk = 0x56, // V key
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
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
                        wVk = 0x56, // V key
                        wScan = 0,
                        dwFlags = Hotkeys.Win32Interop.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
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
                        wVk = 0x11, // VK_CONTROL
                        wScan = 0,
                        dwFlags = Hotkeys.Win32Interop.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
            
            // Send the input
            var structSize = System.Runtime.InteropServices.Marshal.SizeOf<Hotkeys.Win32Interop.INPUT>();
            var result = Hotkeys.Win32Interop.SendInput(4, inputs, structSize);
            
            _logger.LogDebug("SendInput returned {Result} (expected 4)", result);
            
            return result == 4;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in SendCtrlV");
            return false;
        }
    }
}

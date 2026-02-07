using System.ComponentModel;
using System.Runtime.InteropServices;
using LisperFlow.Hotkeys;
using Microsoft.Extensions.Logging;

namespace LisperFlow.TextInjection;

public class RealTimeTextInjector : IDisposable
{
    private readonly TypingCommandQueue _commandQueue;
    private readonly int _typingDelayMs;
    private readonly ILogger<RealTimeTextInjector> _logger;
    private Task? _typingTask;
    private CancellationTokenSource? _cts;
    private IntPtr _targetWindowHandle;
    private int _typedCharacterCount;
    private char? _lastTypedChar;
    
    public RealTimeTextInjector(
        TypingCommandQueue commandQueue,
        int typingDelayMs,
        ILogger<RealTimeTextInjector> logger)
    {
        _commandQueue = commandQueue;
        _typingDelayMs = typingDelayMs;
        _logger = logger;
    }
    
    public void Start(IntPtr targetWindow)
    {
        if (_typingTask != null)
        {
            throw new InvalidOperationException("Real-time typing already started.");
        }
        
        _targetWindowHandle = targetWindow;
        _typedCharacterCount = 0;
        _lastTypedChar = null;
        _cts = new CancellationTokenSource();
        _typingTask = TypingLoopAsync(_cts.Token);
        
        _logger.LogInformation("Real-time typing started for window {Handle}", targetWindow);
    }
    
    public async Task StopAsync()
    {
        if (_typingTask == null) return;
        
        _cts?.Cancel();
        try
        {
            await _typingTask;
        }
        catch (OperationCanceledException) { }
        
        _typingTask = null;
        _cts?.Dispose();
        _cts = null;
        
        _logger.LogInformation("Real-time typing stopped");
    }
    
    private async Task TypingLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var command = await _commandQueue.DequeueAsync(cancellationToken);
                _logger.LogTrace("Dequeued typing command: {Type}", command.GetType().Name);
                
                // Wait until the target window is focused (with restore attempts)
                if (!await EnsureTargetWindowFocusedAsync(cancellationToken))
                {
                    _logger.LogWarning("Could not restore focus to target window after retries — dropping command");
                    continue;
                }
                
                switch (command)
                {
                    case TypeWordCommand wordCmd:
                        await TypeWordAsync(wordCmd.Word, cancellationToken);
                        break;
                    case CorrectionCommand correctionCmd:
                        await ApplyCorrectionAsync(correctionCmd, cancellationToken);
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in real-time typing loop");
            }
        }
    }
    
    /// <summary>
    /// Ensures the target window has keyboard focus. Tries to restore focus
    /// up to <see cref="MaxFocusRetries"/> times before giving up.
    /// </summary>
    private const int MaxFocusRetries = 10;      // ~1 s total wait
    private const int FocusRetryDelayMs = 100;
    
    private async Task<bool> EnsureTargetWindowFocusedAsync(CancellationToken cancellationToken)
    {
        if (_targetWindowHandle == IntPtr.Zero) return false;
        
        for (int attempt = 0; attempt < MaxFocusRetries; attempt++)
        {
            if (Win32Interop.GetForegroundWindow() == _targetWindowHandle)
                return true;
            
            if (attempt == 0)
            {
                _logger.LogDebug("Target window lost focus — attempting to restore");
            }
            
            // Attach our thread input to the foreground thread so
            // SetForegroundWindow is allowed by the OS.
            uint currentThreadId = Win32Interop.GetCurrentThreadId();
            uint foregroundThreadId = Win32Interop.GetWindowThreadProcessId(
                Win32Interop.GetForegroundWindow(), out _);
            
            bool attached = false;
            if (currentThreadId != foregroundThreadId)
            {
                attached = Win32Interop.AttachThreadInput(currentThreadId, foregroundThreadId, true);
            }
            
            try
            {
                Win32Interop.SetForegroundWindow(_targetWindowHandle);
            }
            finally
            {
                if (attached)
                {
                    Win32Interop.AttachThreadInput(currentThreadId, foregroundThreadId, false);
                }
            }
            
            await Task.Delay(FocusRetryDelayMs, cancellationToken);
        }
        
        return Win32Interop.GetForegroundWindow() == _targetWindowHandle;
    }
    
    private async Task TypeWordAsync(string word, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(word)) return;
        
        _logger.LogDebug("Typing word: '{Word}' to window {Handle}", word, _targetWindowHandle);
        string textToType = ShouldAddSpaceBefore(word) ? " " + word : word;
        
        int typedChars = TypeString(textToType);
        if (typedChars < textToType.Length)
        {
            _logger.LogWarning(
                "Only typed {Typed}/{Total} chars for word '{Word}'",
                typedChars,
                textToType.Length,
                word);
            
            if (!cancellationToken.IsCancellationRequested &&
                await EnsureTargetWindowFocusedAsync(cancellationToken))
            {
                string remaining = textToType.Substring(typedChars);
                if (!string.IsNullOrEmpty(remaining))
                {
                    int retryTyped = TypeString(remaining);
                    if (retryTyped < remaining.Length)
                    {
                        _logger.LogWarning(
                            "Retry only typed {Typed}/{Total} chars for word '{Word}'",
                            retryTyped,
                            remaining.Length,
                            word);
                    }
                }
            }
        }
        
        await Task.Delay(_typingDelayMs, cancellationToken);
    }
    
    private async Task ApplyCorrectionAsync(CorrectionCommand correction, CancellationToken cancellationToken)
    {
        if (correction.CharactersToDelete <= 0 && string.IsNullOrEmpty(correction.NewText))
        {
            return;
        }
        
        // Only apply corrections near the end of the typed buffer
        int expectedEndPosition = _typedCharacterCount;
        if (correction.Position + correction.CharactersToDelete < expectedEndPosition - 2)
        {
            _logger.LogDebug(
                "Skipping correction outside tail window (pos {Pos}, typed {Typed})",
                correction.Position,
                _typedCharacterCount);
            return;
        }
        
        for (int i = 0; i < correction.CharactersToDelete; i++)
        {
            SendBackspace();
            _typedCharacterCount = Math.Max(0, _typedCharacterCount - 1);
            await Task.Delay(10, cancellationToken);
        }
        
        if (!string.IsNullOrEmpty(correction.NewText))
        {
            TypeString(correction.NewText);
        }
        
        await Task.Delay(_typingDelayMs, cancellationToken);
    }
    
    private int TypeString(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        
        var inputs = new List<Win32Interop.INPUT>(text.Length * 4);
        var inputCountsPerChar = new List<int>(text.Length);
        
        foreach (char c in text)
        {
            int inputsBefore = inputs.Count;
            var (vkCode, needsShift) = CharToVirtualKey(c);
            if (vkCode != 0)
            {
                if (needsShift)
                {
                    inputs.Add(new Win32Interop.INPUT
                    {
                        type = Win32Interop.INPUT_KEYBOARD,
                        Input = new Win32Interop.InputUnion
                        {
                            ki = new Win32Interop.KEYBDINPUT
                            {
                                wVk = 0x10,
                                wScan = 0,
                                dwFlags = 0
                            }
                        }
                    });
                }
                
                inputs.Add(new Win32Interop.INPUT
                {
                    type = Win32Interop.INPUT_KEYBOARD,
                    Input = new Win32Interop.InputUnion
                    {
                        ki = new Win32Interop.KEYBDINPUT
                        {
                            wVk = vkCode,
                            wScan = 0,
                            dwFlags = 0
                        }
                    }
                });
                
                inputs.Add(new Win32Interop.INPUT
                {
                    type = Win32Interop.INPUT_KEYBOARD,
                    Input = new Win32Interop.InputUnion
                    {
                        ki = new Win32Interop.KEYBDINPUT
                        {
                            wVk = vkCode,
                            wScan = 0,
                            dwFlags = Win32Interop.KEYEVENTF_KEYUP
                        }
                    }
                });
                
                if (needsShift)
                {
                    inputs.Add(new Win32Interop.INPUT
                    {
                        type = Win32Interop.INPUT_KEYBOARD,
                        Input = new Win32Interop.InputUnion
                        {
                            ki = new Win32Interop.KEYBDINPUT
                            {
                                wVk = 0x10,
                                wScan = 0,
                                dwFlags = Win32Interop.KEYEVENTF_KEYUP
                            }
                        }
                    });
                }
            }
            else
            {
                inputs.Add(new Win32Interop.INPUT
                {
                    type = Win32Interop.INPUT_KEYBOARD,
                    Input = new Win32Interop.InputUnion
                    {
                        ki = new Win32Interop.KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = c,
                            dwFlags = Win32Interop.KEYEVENTF_UNICODE
                        }
                    }
                });
                
                inputs.Add(new Win32Interop.INPUT
                {
                    type = Win32Interop.INPUT_KEYBOARD,
                    Input = new Win32Interop.InputUnion
                    {
                        ki = new Win32Interop.KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = c,
                            dwFlags = Win32Interop.KEYEVENTF_UNICODE | Win32Interop.KEYEVENTF_KEYUP
                        }
                    }
                });
            }
            
            inputCountsPerChar.Add(inputs.Count - inputsBefore);
        }
        
        uint result = Win32Interop.SendInput(
            (uint)inputs.Count,
            inputs.ToArray(),
            Marshal.SizeOf<Win32Interop.INPUT>());
        
        if (result != inputs.Count)
        {
            int error = Marshal.GetLastWin32Error();
            var errorMessage = new Win32Exception(error).Message;
            _logger.LogWarning(
                "SendInput only sent {Sent}/{Total} inputs (err {Error}: {Message})",
                result,
                inputs.Count,
                error,
                errorMessage);
        }
        
        int sentInputs = (int)result;
        int sentChars = 0;
        int remainingInputs = sentInputs;
        foreach (int perChar in inputCountsPerChar)
        {
            if (remainingInputs < perChar)
            {
                break;
            }
            
            remainingInputs -= perChar;
            sentChars++;
        }
        
        if (sentChars > 0)
        {
            _typedCharacterCount += sentChars;
            _lastTypedChar = text[sentChars - 1];
        }
        
        if (sentInputs != inputs.Count)
        {
            _logger.LogWarning(
                "SendInput returned partial count {Sent}/{Total} for text length {Length}",
                sentInputs,
                inputs.Count,
                text.Length);
        }
        
        return sentChars;
    }

    private static (ushort vkCode, bool needsShift) CharToVirtualKey(char c)
    {
        if (c >= 'a' && c <= 'z')
            return ((ushort)(0x41 + (c - 'a')), false);
        if (c >= 'A' && c <= 'Z')
            return ((ushort)(0x41 + (c - 'A')), true);
        
        if (c >= '0' && c <= '9')
            return ((ushort)(0x30 + (c - '0')), false);
        
        return c switch
        {
            ' ' => (0x20, false),
            '!' => (0x31, true),
            '@' => (0x32, true),
            '#' => (0x33, true),
            '$' => (0x34, true),
            '%' => (0x35, true),
            '^' => (0x36, true),
            '&' => (0x37, true),
            '*' => (0x38, true),
            '(' => (0x39, true),
            ')' => (0x30, true),
            '-' => (0xBD, false),
            '_' => (0xBD, true),
            '=' => (0xBB, false),
            '+' => (0xBB, true),
            '[' => (0xDB, false),
            '{' => (0xDB, true),
            ']' => (0xDD, false),
            '}' => (0xDD, true),
            '\\' => (0xDC, false),
            '|' => (0xDC, true),
            ';' => (0xBA, false),
            ':' => (0xBA, true),
            '\'' => (0xDE, false),
            '"' => (0xDE, true),
            ',' => (0xBC, false),
            '<' => (0xBC, true),
            '.' => (0xBE, false),
            '>' => (0xBE, true),
            '/' => (0xBF, false),
            '?' => (0xBF, true),
            '`' => (0xC0, false),
            '~' => (0xC0, true),
            _ => (0, false)
        };
    }
    
    private void SendBackspace()
    {
        var inputs = new Win32Interop.INPUT[2];
        
        inputs[0] = new Win32Interop.INPUT
        {
            type = Win32Interop.INPUT_KEYBOARD,
            Input = new Win32Interop.InputUnion
            {
                ki = new Win32Interop.KEYBDINPUT
                {
                    wVk = 0x08,
                    wScan = 0,
                    dwFlags = 0
                }
            }
        };
        
        inputs[1] = new Win32Interop.INPUT
        {
            type = Win32Interop.INPUT_KEYBOARD,
            Input = new Win32Interop.InputUnion
            {
                ki = new Win32Interop.KEYBDINPUT
                {
                    wVk = 0x08,
                    wScan = 0,
                    dwFlags = Win32Interop.KEYEVENTF_KEYUP
                }
            }
        };
        
        Win32Interop.SendInput(2, inputs, Marshal.SizeOf<Win32Interop.INPUT>());
    }
    
    private bool IsTargetWindowFocused()
    {
        return Win32Interop.GetForegroundWindow() == _targetWindowHandle;
    }
    
    private bool ShouldAddSpaceBefore(string word)
    {
        if (string.IsNullOrEmpty(word)) return false;
        if (_typedCharacterCount == 0) return false;
        
        char first = word[0];
        if (char.IsPunctuation(first) || first == '\'' || first == '"' || first == ')' || first == ']' || first == '}')
        {
            return false;
        }
        
        if (_lastTypedChar.HasValue && char.IsWhiteSpace(_lastTypedChar.Value))
        {
            return false;
        }
        
        return true;
    }
    
    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }
}

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
                
                if (!IsTargetWindowFocused())
                {
                    _logger.LogDebug("Target window not focused, waiting");
                    await Task.Delay(100, cancellationToken);
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
    
    private async Task TypeWordAsync(string word, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(word)) return;
        
        _logger.LogTrace("Typing word: {Word}", word);
        if (ShouldAddSpaceBefore(word))
        {
            TypeString(" ");
        }
        
        TypeString(word);
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
    
    private void TypeString(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        
        var inputs = new List<Win32Interop.INPUT>();
        
        foreach (char c in text)
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
            
            _typedCharacterCount++;
            _lastTypedChar = c;
        }
        
        if (inputs.Count > 0)
        {
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
        }
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

using System.Windows;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;

namespace LisperFlow.Hotkeys;

/// <summary>
/// Hidden window that listens for Windows messages (WM_HOTKEY)
/// </summary>
public class WindowMessageListener : IDisposable
{
    private HwndSource? _hwndSource;
    private readonly ILogger<WindowMessageListener>? _logger;
    private bool _disposed;
    
    /// <summary>
    /// Window handle for message receiving
    /// </summary>
    public IntPtr Handle => _hwndSource?.Handle ?? IntPtr.Zero;
    
    /// <summary>
    /// Event raised when a Windows message is received
    /// </summary>
    public event EventHandler<WindowMessageEventArgs>? MessageReceived;
    
    public WindowMessageListener(ILogger<WindowMessageListener>? logger = null)
    {
        _logger = logger;
        
        // Must create window on UI thread
        if (Application.Current?.Dispatcher.CheckAccess() == true)
        {
            InitializeWindow();
        }
        else
        {
            Application.Current?.Dispatcher.Invoke(InitializeWindow);
        }
    }
    
    private void InitializeWindow()
    {
        try
        {
            var parameters = new HwndSourceParameters("LisperFlowHotkeyWindow")
            {
                Width = 0,
                Height = 0,
                PositionX = 0,
                PositionY = 0,
                WindowStyle = 0 // Hidden window with no visible presence
            };
            
            _hwndSource = new HwndSource(parameters);
            _hwndSource.AddHook(WndProc);
            
            _logger?.LogDebug("Message listener window created with handle: {Handle}", _hwndSource.Handle);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create message listener window");
            throw;
        }
    }
    
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Raise event for all messages, let listeners filter
        MessageReceived?.Invoke(this, new WindowMessageEventArgs
        {
            Handle = hwnd,
            Message = msg,
            WParam = wParam,
            LParam = lParam
        });
        
        return IntPtr.Zero;
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        if (_hwndSource != null)
        {
            if (Application.Current?.Dispatcher.CheckAccess() == true)
            {
                CleanupWindow();
            }
            else
            {
                Application.Current?.Dispatcher.Invoke(CleanupWindow);
            }
        }
        
        _logger?.LogDebug("Message listener disposed");
    }
    
    private void CleanupWindow()
    {
        _hwndSource?.RemoveHook(WndProc);
        _hwndSource?.Dispose();
        _hwndSource = null;
    }
}

/// <summary>
/// Event arguments for Windows messages
/// </summary>
public class WindowMessageEventArgs : EventArgs
{
    public IntPtr Handle { get; set; }
    public int Message { get; set; }
    public IntPtr WParam { get; set; }
    public IntPtr LParam { get; set; }
}

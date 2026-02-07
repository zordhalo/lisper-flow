using System.Diagnostics;
using System.Text;
using LisperFlow.Hotkeys;
using Microsoft.Extensions.Logging;

namespace LisperFlow.TextInjection;

/// <summary>
/// Detects the currently focused UI element
/// </summary>
public class FocusedElementDetector
{
    private readonly ILogger<FocusedElementDetector> _logger;
    
    public FocusedElementDetector(ILogger<FocusedElementDetector> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Get information about the currently focused element
    /// </summary>
    public FocusedElementInfo? GetFocusedElement()
    {
        try
        {
            // Get foreground window
            IntPtr foregroundWindow = Win32Interop.GetForegroundWindow();
            
            if (foregroundWindow == IntPtr.Zero)
            {
                _logger.LogWarning("No foreground window detected");
                return null;
            }
            
            // Get window information
            uint processId;
            Win32Interop.GetWindowThreadProcessId(foregroundWindow, out processId);
            
            string processName = "";
            try
            {
                var process = Process.GetProcessById((int)processId);
                processName = process.ProcessName;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get process name for PID {ProcessId}", processId);
            }
            
            // Get window title
            string windowTitle = GetWindowText(foregroundWindow);
            
            // Get window class (helps identify control type)
            string className = GetClassName(foregroundWindow);
            
            var elementInfo = new FocusedElementInfo
            {
                WindowHandle = foregroundWindow,
                ProcessId = (int)processId,
                ProcessName = processName,
                WindowTitle = windowTitle,
                ControlType = className,
                // For now, assume any window can receive text input
                // More sophisticated detection would use UI Automation
                IsTextInputCapable = true
            };
            
            _logger.LogDebug(
                "Focused element: Process={Process}, Title={Title}, Class={Class}",
                elementInfo.ProcessName,
                elementInfo.WindowTitle,
                elementInfo.ControlType);
            
            return elementInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect focused element");
            return null;
        }
    }
    
    private static string GetWindowText(IntPtr hWnd)
    {
        int length = Win32Interop.GetWindowTextLength(hWnd);
        if (length == 0) return "";
        
        var sb = new StringBuilder(length + 1);
        Win32Interop.GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }
    
    private static string GetClassName(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        Win32Interop.GetClassName(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }
}

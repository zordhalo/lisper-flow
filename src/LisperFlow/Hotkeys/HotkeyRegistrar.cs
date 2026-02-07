using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace LisperFlow.Hotkeys;

/// <summary>
/// Service for registering and managing system-wide hotkeys.
/// </summary>
public class HotkeyRegistrar : IDisposable
{
    private readonly Dictionary<int, HotkeyConfig> _registeredHotkeys;
    private readonly WindowMessageListener _messageListener;
    private readonly ILogger<HotkeyRegistrar> _logger;
    private int _nextHotkeyId = 1;
    private bool _disposed;
    
    /// <summary>
    /// Event raised when a registered hotkey is pressed
    /// </summary>
    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;
    
    public HotkeyRegistrar(ILogger<HotkeyRegistrar> logger)
    {
        _logger = logger;
        _registeredHotkeys = new Dictionary<int, HotkeyConfig>();
        _messageListener = new WindowMessageListener(null);
        _messageListener.MessageReceived += OnMessageReceived;
        
        _logger.LogInformation("Hotkey registrar initialized");
    }
    
    /// <summary>
    /// Register a global hotkey.
    /// </summary>
    /// <param name="config">Hotkey configuration</param>
    /// <returns>True if registration successful</returns>
    /// <exception cref="HotkeyAlreadyRegisteredException">Thrown if hotkey is already registered by another app</exception>
    public bool RegisterHotkey(HotkeyConfig config)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(HotkeyRegistrar));
        
        int hotkeyId = _nextHotkeyId++;
        config.Id = hotkeyId;
        
        uint modifierFlags = config.GetModifierFlags();
        uint vkCode = (uint)config.VirtualKeyCode;
        
        bool success = Win32Interop.RegisterHotKey(
            _messageListener.Handle,
            hotkeyId,
            modifierFlags,
            vkCode);
        
        if (success)
        {
            _registeredHotkeys[hotkeyId] = config;
            
            _logger.LogInformation(
                "Registered hotkey: {Name} ({Display}) with ID {Id}",
                config.Name,
                config.GetDisplayString(),
                hotkeyId);
            
            return true;
        }
        else
        {
            int error = Marshal.GetLastWin32Error();
            
            _logger.LogError(
                "Failed to register hotkey {Name} ({Display}): Win32 error {Error}",
                config.Name,
                config.GetDisplayString(),
                error);
            
            // Error 1409 = Hotkey already registered
            if (error == Win32Interop.ERROR_HOTKEY_ALREADY_REGISTERED)
            {
                throw new HotkeyAlreadyRegisteredException(
                    $"Hotkey {config.GetDisplayString()} is already registered by another application",
                    config);
            }
            
            return false;
        }
    }
    
    /// <summary>
    /// Unregister a hotkey by its ID.
    /// </summary>
    public bool UnregisterHotkey(int hotkeyId)
    {
        if (_disposed) return false;
        
        if (_registeredHotkeys.TryGetValue(hotkeyId, out var config))
        {
            bool success = Win32Interop.UnregisterHotKey(_messageListener.Handle, hotkeyId);
            
            if (success)
            {
                _registeredHotkeys.Remove(hotkeyId);
                _logger.LogInformation(
                    "Unregistered hotkey: {Name} with ID {Id}",
                    config.Name,
                    hotkeyId);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to unregister hotkey ID {Id}",
                    hotkeyId);
            }
            
            return success;
        }
        
        return false;
    }
    
    /// <summary>
    /// Unregister all registered hotkeys.
    /// </summary>
    public void UnregisterAll()
    {
        foreach (var hotkeyId in _registeredHotkeys.Keys.ToList())
        {
            UnregisterHotkey(hotkeyId);
        }
    }
    
    /// <summary>
    /// Try to register a hotkey, returning suggested alternatives if it fails.
    /// </summary>
    public (bool Success, List<HotkeyConfig> Alternatives) TryRegisterWithFallback(HotkeyConfig config)
    {
        try
        {
            bool success = RegisterHotkey(config);
            return (success, new List<HotkeyConfig>());
        }
        catch (HotkeyAlreadyRegisteredException)
        {
            // Generate alternative suggestions
            var alternatives = GenerateAlternatives(config);
            return (false, alternatives);
        }
    }
    
    private List<HotkeyConfig> GenerateAlternatives(HotkeyConfig original)
    {
        var alternatives = new List<HotkeyConfig>();
        
        // Try with Shift added
        if (!original.UseShift)
        {
            alternatives.Add(new HotkeyConfig
            {
                Name = original.Name,
                UseControl = original.UseControl,
                UseAlt = original.UseAlt,
                UseShift = true,
                UseWin = original.UseWin,
                VirtualKeyCode = original.VirtualKeyCode
            });
        }
        
        // Try Alt instead of Ctrl
        if (original.UseControl && !original.UseAlt)
        {
            alternatives.Add(new HotkeyConfig
            {
                Name = original.Name,
                UseControl = false,
                UseAlt = true,
                UseShift = original.UseShift,
                UseWin = original.UseWin,
                VirtualKeyCode = original.VirtualKeyCode
            });
        }
        
        // Try F-keys
        for (int fKey = 0x70; fKey <= 0x73; fKey++) // F1-F4
        {
            alternatives.Add(new HotkeyConfig
            {
                Name = original.Name,
                UseControl = true,
                UseAlt = false,
                UseShift = false,
                UseWin = false,
                VirtualKeyCode = fKey
            });
        }
        
        return alternatives;
    }
    
    private void OnMessageReceived(object? sender, WindowMessageEventArgs e)
    {
        if (e.Message == Win32Interop.WM_HOTKEY)
        {
            int hotkeyId = e.WParam.ToInt32();
            
            if (_registeredHotkeys.TryGetValue(hotkeyId, out var config))
            {
                _logger.LogDebug(
                    "Hotkey pressed: {Name} ({Display})",
                    config.Name,
                    config.GetDisplayString());
                
                // Raise event on UI thread
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs
                    {
                        HotkeyId = hotkeyId,
                        Name = config.Name,
                        Config = config,
                        Timestamp = DateTime.UtcNow
                    });
                });
            }
        }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        UnregisterAll();
        
        _messageListener.MessageReceived -= OnMessageReceived;
        _messageListener.Dispose();
        
        _logger.LogInformation("Hotkey registrar disposed");
    }
}

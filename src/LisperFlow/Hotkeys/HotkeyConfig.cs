namespace LisperFlow.Hotkeys;

/// <summary>
/// Configuration for a registered hotkey
/// </summary>
public class HotkeyConfig
{
    /// <summary>
    /// Unique identifier for this hotkey registration
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Display name for this hotkey
    /// </summary>
    public string Name { get; set; } = "Unnamed";
    
    /// <summary>
    /// Use Control modifier
    /// </summary>
    public bool UseControl { get; set; }
    
    /// <summary>
    /// Use Alt modifier
    /// </summary>
    public bool UseAlt { get; set; }
    
    /// <summary>
    /// Use Shift modifier
    /// </summary>
    public bool UseShift { get; set; }
    
    /// <summary>
    /// Use Windows key modifier
    /// </summary>
    public bool UseWin { get; set; }
    
    /// <summary>
    /// Virtual key code for the main key
    /// </summary>
    public int VirtualKeyCode { get; set; }
    
    /// <summary>
    /// Gets the combined modifier flags for Win32 API
    /// </summary>
    public uint GetModifierFlags()
    {
        uint flags = Win32Interop.MOD_NOREPEAT; // Always prevent key repeat
        
        if (UseControl) flags |= Win32Interop.MOD_CONTROL;
        if (UseAlt) flags |= Win32Interop.MOD_ALT;
        if (UseShift) flags |= Win32Interop.MOD_SHIFT;
        if (UseWin) flags |= Win32Interop.MOD_WIN;
        
        return flags;
    }
    
    /// <summary>
    /// Gets a display string for this hotkey (e.g., "Ctrl+Win+Space")
    /// </summary>
    public string GetDisplayString()
    {
        var parts = new List<string>();
        
        if (UseControl) parts.Add("Ctrl");
        if (UseAlt) parts.Add("Alt");
        if (UseShift) parts.Add("Shift");
        if (UseWin) parts.Add("Win");
        
        parts.Add(GetKeyName(VirtualKeyCode));
        
        return string.Join("+", parts);
    }
    
    private static string GetKeyName(int vkCode)
    {
        return vkCode switch
        {
            0x20 => "Space",
            0x0D => "Enter",
            0x1B => "Escape",
            0x09 => "Tab",
            0x08 => "Backspace",
            >= 0x70 and <= 0x7B => $"F{vkCode - 0x6F}",
            >= 0x41 and <= 0x5A => ((char)vkCode).ToString(), // A-Z
            >= 0x30 and <= 0x39 => ((char)vkCode).ToString(), // 0-9
            _ => $"VK_{vkCode:X2}"
        };
    }
    
    /// <summary>
    /// Create a HotkeyConfig from configuration settings
    /// </summary>
    public static HotkeyConfig FromSettings(Configuration.HotkeySettings settings, string name = "Dictation")
    {
        return new HotkeyConfig
        {
            Name = name,
            UseControl = settings.UseControl,
            UseAlt = settings.UseAlt,
            UseShift = settings.UseShift,
            UseWin = settings.UseWin,
            VirtualKeyCode = settings.VirtualKeyCode
        };
    }
}

/// <summary>
/// Event arguments for hotkey pressed events
/// </summary>
public class HotkeyPressedEventArgs : EventArgs
{
    public int HotkeyId { get; set; }
    public string Name { get; set; } = "";
    public HotkeyConfig? Config { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Exception thrown when a hotkey is already registered by another application
/// </summary>
public class HotkeyAlreadyRegisteredException : Exception
{
    public HotkeyConfig? Config { get; }
    
    public HotkeyAlreadyRegisteredException(string message) : base(message) { }
    
    public HotkeyAlreadyRegisteredException(string message, HotkeyConfig config) : base(message)
    {
        Config = config;
    }
}

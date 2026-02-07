using System.Runtime.InteropServices;

namespace LisperFlow.Hotkeys;

/// <summary>
/// Win32 API interop for global hotkey registration
/// </summary>
public static class Win32Interop
{
    #region Hotkey Modifiers
    
    /// <summary>Alt key modifier</summary>
    public const uint MOD_ALT = 0x0001;
    
    /// <summary>Control key modifier</summary>
    public const uint MOD_CONTROL = 0x0002;
    
    /// <summary>Shift key modifier</summary>
    public const uint MOD_SHIFT = 0x0004;
    
    /// <summary>Windows key modifier</summary>
    public const uint MOD_WIN = 0x0008;
    
    /// <summary>Changes the hotkey behavior so that the keyboard auto-repeat does not yield another hotkey notification</summary>
    public const uint MOD_NOREPEAT = 0x4000;
    
    #endregion
    
    #region Virtual Key Codes
    
    public const int VK_SPACE = 0x20;
    public const int VK_RETURN = 0x0D;
    public const int VK_ESCAPE = 0x1B;
    public const int VK_F1 = 0x70;
    public const int VK_F12 = 0x7B;
    
    #endregion
    
    #region Window Messages
    
    /// <summary>Posted when the user presses a registered hot key</summary>
    public const int WM_HOTKEY = 0x0312;
    
    #endregion
    
    #region Hotkey Functions
    
    /// <summary>
    /// Defines a system-wide hot key.
    /// </summary>
    /// <param name="hWnd">Handle to the window that will receive WM_HOTKEY messages</param>
    /// <param name="id">The identifier of the hot key</param>
    /// <param name="fsModifiers">The keys that must be pressed in combination with the key specified by the vk parameter</param>
    /// <param name="vk">The virtual-key code of the hot key</param>
    /// <returns>True if the function succeeds, otherwise false</returns>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    
    /// <summary>
    /// Frees a hot key previously registered by the calling thread.
    /// </summary>
    /// <param name="hWnd">Handle to the window associated with the hot key to be freed</param>
    /// <param name="id">The identifier of the hot key to be freed</param>
    /// <returns>True if the function succeeds, otherwise false</returns>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    
    #endregion
    
    #region Window Functions
    
    /// <summary>
    /// Retrieves a handle to the foreground window
    /// </summary>
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
    
    /// <summary>
    /// Retrieves the identifier of the thread that created the specified window
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    
    /// <summary>
    /// Retrieves the length of the specified window's title bar text
    /// </summary>
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int GetWindowTextLength(IntPtr hWnd);
    
    /// <summary>
    /// Copies the text of the specified window's title bar into a buffer
    /// </summary>
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
    
    /// <summary>
    /// Retrieves the name of the class to which the specified window belongs
    /// </summary>
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);
    
    #endregion
    
    #region Input Functions
    
    /// <summary>
    /// Synthesizes keystrokes, mouse motions, and button clicks.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public InputUnion Input;
    }
    
    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
    
    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_UNICODE = 0x0004;
    
    #endregion
    
    #region Clipboard Functions
    
    [DllImport("user32.dll")]
    public static extern bool OpenClipboard(IntPtr hWndNewOwner);
    
    [DllImport("user32.dll")]
    public static extern bool CloseClipboard();
    
    [DllImport("user32.dll")]
    public static extern bool EmptyClipboard();
    
    [DllImport("user32.dll")]
    public static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
    
    [DllImport("user32.dll")]
    public static extern IntPtr GetClipboardData(uint uFormat);
    
    public const uint CF_UNICODETEXT = 13;
    
    #endregion
    
    #region Error Codes
    
    /// <summary>
    /// The hot key is already registered (ERROR_HOTKEY_ALREADY_REGISTERED)
    /// </summary>
    public const int ERROR_HOTKEY_ALREADY_REGISTERED = 1409;
    
    #endregion
}

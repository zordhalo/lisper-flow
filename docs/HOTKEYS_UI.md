## Hotkeys and UI

### Hotkey System
The hotkey system uses a hidden window to receive `WM_HOTKEY` messages.

Key classes:
- `HotkeyRegistrar`: registers and manages hotkeys
- `WindowMessageListener`: hidden window for message pump
- `Win32Interop`: P/Invoke definitions and constants
- `HotkeyConfig`: configuration and display string builder

Hotkey registration uses `MOD_NOREPEAT` to prevent repeated triggers.

### System Tray UI
`SystemTrayViewModel` exposes app state for the tray UI:
- Status text and color
- Last transcript preview
- Hotkey display

### Settings Window
`SettingsWindow` allows configuring:
- Hotkey
- ASR provider (cloud/local)
- LLM provider (cloud/local)
- Model paths
- Microphone device

Streaming settings are not yet exposed via the UI; use `appsettings.json`.

### Main Window
`MainWindow` is a placeholder WPF window. The app runs primarily in the tray.

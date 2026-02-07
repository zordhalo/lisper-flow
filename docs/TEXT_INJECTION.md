## Text Injection

Lisper-Flow injects recognized text into the currently focused window using
multiple strategies. Batch mode uses strategy selection; streaming mode uses
real-time typing.

### Batch Injection
`InjectionStrategySelector` chooses a strategy:
1. `ClipboardPasteInjector` (fast, universal)
2. `SendInputInjector` (fallback, slower)

#### ClipboardPasteInjector
- Copies text to clipboard
- Sends `Ctrl+V`
- Restores prior clipboard content

#### SendInputInjector
- Types each character with `SendInput`
- Adds small delays to avoid dropped input

### Streaming Injection
`RealTimeTextInjector` consumes `TypingCommandQueue`.
It:
- Types words with per-word delay
- Applies tail corrections with backspace
- Skips typing when target window loses focus

### Spacing Rules
`RealTimeTextInjector` avoids inserting spaces before punctuation and when
the last typed character is already whitespace.

### Focus Detection
`FocusedElementDetector` determines the current active window using Win32 APIs.

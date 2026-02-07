## Architecture Overview

Lisper-Flow is a Windows WPF application that captures microphone audio,
transcribes speech to text, optionally enhances the transcript, and injects
the text into the currently focused application. It supports both batch
recording and real-time streaming.

### High-Level Flow

Batch Mode:
1. Hotkey toggles recording
2. Audio capture collects samples into a buffer
3. ASR transcribes the full audio block
4. Optional LLM enhancement is applied
5. Text is injected into the focused app

Streaming Mode:
1. Hotkey starts streaming session
2. Audio capture emits 100ms chunks
3. Streaming ASR provider emits partial and final transcripts
4. Transcript synchronizer computes new words and corrections
5. Real-time text injector types words incrementally

### Key Components
- **Audio Capture**: `AudioCaptureService`, `WasapiCaptureManager`,
  `VoiceActivityDetector`, `RingBufferManager`
- **ASR**:
  - Batch: `IAsrProvider`, `OpenAIWhisperProvider`, `WhisperOnnxProvider`
  - Streaming: `IStreamingAsrProvider`, `AzureSpeechStreamProvider`,
    `DeepgramStreamProvider`
- **Streaming Orchestration**: `StreamingCoordinator`, `TranscriptSynchronizer`
- **Text Injection**:
  - Batch: `InjectionStrategySelector`, `ClipboardPasteInjector`,
    `SendInputInjector`
  - Streaming: `RealTimeTextInjector`, `TypingCommandQueue`
- **UI/UX**: `App.xaml.cs`, `SystemTrayViewModel`, `SettingsWindow`
- **Hotkeys**: `HotkeyRegistrar`, `WindowMessageListener`, `Win32Interop`

### Dependency Flow
- `App.xaml.cs` configures DI and loads `AppSettings`
- `DictationService` orchestrates batch and streaming modes
- `AudioCaptureService` provides both batch buffering and streaming chunking

### Error Handling
Errors are logged via Serilog. Failures in batch or streaming propagate through
`DictationService.ErrorOccurred` for UI handling.

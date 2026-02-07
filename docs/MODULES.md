## Module Overview

### Application Boot
- `App.xaml.cs`: loads settings, configures DI, starts `DictationService`
- `App.xaml`: WPF application declaration

### Services
- `Services/DictationService.cs`: main orchestrator for batch and streaming
  - Handles hotkey toggle
  - Batch pipeline: capture -> ASR -> LLM -> inject
  - Streaming pipeline: coordinator -> sync -> typing

### Audio Capture
- `AudioCapture/AudioCaptureService.cs`: main capture service, VAD, buffer
- `AudioCapture/WasapiCaptureManager.cs`: WASAPI capture via NAudio
- `AudioCapture/VoiceActivityDetector.cs`: Silero VAD with energy fallback
- `AudioCapture/RingBufferManager.cs`: pre-roll buffer
- `AudioCapture/AudioConfiguration.cs`: audio settings and derived values

### ASR (Batch)
- `ASR/IAsrProvider.cs`: batch ASR contract
- `ASR/OpenAIWhisperProvider.cs`: OpenAI Whisper API
- `ASR/WhisperOnnxProvider.cs`: local Whisper ONNX placeholder

### Streaming
- `Streaming/IStreamingAsrProvider.cs`: streaming ASR contract
- `Streaming/IAudioStreamProvider.cs`: streaming audio contract
- `Streaming/StreamingCoordinator.cs`: audio -> ASR orchestration
- `Streaming/TranscriptSynchronizer.cs`: diff logic for partials
- `Streaming/Models/*`: audio and transcript event models
- `Streaming/AzureSpeechStreamProvider.cs`: Azure SDK streaming
- `Streaming/DeepgramStreamProvider.cs`: Deepgram WebSocket streaming

### Text Injection
- `TextInjection/InjectionStrategySelector.cs`: batch injection chooser
- `TextInjection/FocusedElementDetector.cs`: focused window metadata
- `TextInjection/Strategies/ClipboardPasteInjector.cs`: clipboard-based
- `TextInjection/Strategies/SendInputInjector.cs`: SendInput fallback
- `TextInjection/TypingCommandQueue.cs`: streaming queue
- `TextInjection/RealTimeTextInjector.cs`: streaming typer
- `TextInjection/TypingCommands.cs`: typing command types

### LLM
- `LLM/ILlmProvider.cs`: LLM contract
- `LLM/OpenAIProvider.cs`: OpenAI Chat API
- `LLM/Phi3OnnxProvider.cs`: local Phi-3 placeholder
- `LLM/PromptTemplateEngine.cs`: prompt construction

### Hotkeys
- `Hotkeys/HotkeyRegistrar.cs`: register/unregister hotkeys
- `Hotkeys/WindowMessageListener.cs`: message pump window
- `Hotkeys/Win32Interop.cs`: P/Invoke for hotkeys and input
- `Hotkeys/HotkeyConfig.cs`: hotkey config and helpers

### UI
- `UI/SystemTrayViewModel.cs`: tray state and actions
- `UI/SettingsWindow.xaml.cs`: settings editing
- `MainWindow.xaml.cs`: placeholder main window

### Configuration
- `Configuration/AppSettings.cs`: strongly typed settings
- `appsettings.json`: runtime configuration

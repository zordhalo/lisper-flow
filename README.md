## Lisper-Flow

Windows voice dictation app with batch and real-time streaming transcription, optional LLM enhancement, and multi-strategy text injection. Built on WPF and .NET 8.

### Highlights
- Push-to-talk dictation and streaming (word-by-word typing)
- Cloud ASR (OpenAI Whisper) and streaming ASR (Azure Speech / Deepgram)
- Optional LLM enhancement (OpenAI or local Phi-3 ONNX placeholder)
- Multiple text injection strategies with fallback
- System tray controls and global hotkey support

### Project Layout
- `src/LisperFlow/` - application source
- `docs/` - detailed documentation (architecture, modules, configuration)
- `.vscode/` - launch/tasks for local development

### Requirements
- Windows 10/11
- .NET 8 SDK
- Microphone access enabled
- Network access for cloud providers (OpenAI/Azure/Deepgram)

### Quick Start
1. Install .NET 8 SDK
2. Copy `src/LisperFlow/.env.example` to `src/LisperFlow/.env` and fill in keys
3. Configure non-secret settings in `src/LisperFlow/appsettings.json` (see `docs/CONFIGURATION.md`)
4. Build and run from Visual Studio or:
   - `dotnet build src/LisperFlow/LisperFlow.csproj`
   - `dotnet run --project src/LisperFlow/LisperFlow.csproj`

### Usage
- Hotkey (default): `Ctrl+Space`
- If `Streaming.Enabled = true`, hotkey toggles streaming dictation
- If `Streaming.Enabled = false`, hotkey toggles batch dictation

### Documentation
Start here for the full technical docs:
- `docs/INDEX.md`

### Security Notes
- Store API keys in `src/LisperFlow/.env` or OS environment variables.
- Do not commit secrets; `.env` is ignored by git.

### Current Limitations
- Local Whisper and Phi-3 ONNX providers are placeholders (no full inference)
- Streaming corrections are conservative (tail-only) to avoid disruptive edits
- Settings UI does not yet expose streaming or cloud provider options

### Next Steps (from the plan)
- Complete test scaffolding (unit + integration + performance)
- Add UI feedback for streaming state
- Add confidence thresholds and debouncing for corrections
- Add fallback to batch mode on streaming errors
- Add local streaming ASR option (Whisper streaming)
- Expand settings UI for streaming providers

### Checkpoint
Documentation is complete. Confirm next steps and I will proceed.

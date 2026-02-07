## Configuration

Configuration is loaded from `src/LisperFlow/appsettings.json` into
`AppSettings` in `src/LisperFlow/Configuration/AppSettings.cs`.

### Security
- This file may include API keys. Do not commit secrets.
- Prefer environment variables or a secrets store for production.

### Settings Reference

#### Audio
- `Audio.DeviceId`: specific capture device ID (null uses default)
- `Audio.SampleRate`: target sample rate (default 16000)
- `Audio.VadThreshold`: speech detection threshold
- `Audio.BufferSeconds`: ring buffer capacity
- `Audio.PreRollMs`: pre-roll samples for VAD segments

#### Hotkey
- `Hotkey.UseControl`, `UseAlt`, `UseShift`, `UseWin`
- `Hotkey.VirtualKeyCode`: key code, default `32` (space)

#### ASR (Batch)
- `Asr.Provider`: `Cloud` or `Local`
- `Asr.LocalModelPath`: path to Whisper ONNX files
- `Asr.OpenAiApiKey`: OpenAI API key (cloud)
- `Asr.Language`: language code (e.g., `en`)
- `Asr.UseGpu`: enables DirectML if available

#### LLM
- `Llm.Provider`: `Cloud` or `Local`
- `Llm.LocalModelPath`: path to Phi-3 ONNX files
- `Llm.OpenAiApiKey`: OpenAI API key (cloud)
- `Llm.CloudModel`: model name (default `gpt-4o-mini`)
- `Llm.EnableEnhancement`: toggle enhancement
- `Llm.UseGpu`: enables DirectML if available

#### Streaming
- `Streaming.Enabled`: toggles streaming mode
- `Streaming.Provider`: `AzureSpeech` or `Deepgram`
- `Streaming.ChunkDurationMs`: audio chunk size (default 100ms)
- `Streaming.TypingDelayMs`: word typing delay
- `Streaming.ShowPartialResults`: emit partial transcripts
- `Streaming.ApplyLlmEnhancement`: `AfterFinalization` or `Disabled`

#### Azure Speech
- `AzureSpeech.SubscriptionKey`
- `AzureSpeech.Region` (e.g., `eastus`)
- `AzureSpeech.Language` (e.g., `en-US`)
- `AzureSpeech.EnableProfanityFilter`

#### Deepgram
- `Deepgram.ApiKey`
- `Deepgram.Language` (e.g., `en`)

#### Privacy
- `Privacy.StoreTranscripts`
- `Privacy.SendAnalytics`
- `Privacy.DataRetentionDays`

### Example
```json
{
  "Streaming": {
    "Enabled": true,
    "Provider": "AzureSpeech",
    "ChunkDurationMs": 100,
    "TypingDelayMs": 25,
    "ShowPartialResults": true,
    "ApplyLlmEnhancement": "AfterFinalization"
  }
}
```

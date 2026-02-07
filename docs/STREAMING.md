## Streaming Dictation

Streaming mode provides near real-time word-by-word typing while the user speaks.
It is controlled by `Streaming.Enabled` in `appsettings.json`.

### Data Flow
1. `DictationService` starts streaming
2. `AudioCaptureService` emits 100ms `AudioChunk` events
3. `StreamingCoordinator` forwards audio to `IStreamingAsrProvider`
4. ASR emits partial/final transcripts
5. `TranscriptSynchronizer` computes word additions and corrections
6. `TypingCommandQueue` queues typing commands
7. `RealTimeTextInjector` types text incrementally

### Providers
#### Azure Speech
`AzureSpeechStreamProvider` uses the Azure Speech SDK with a push stream.
It emits:
- Partial results from `Recognizing` events
- Final results from `Recognized` events

#### Deepgram
`DeepgramStreamProvider` uses a WebSocket connection and Deepgram's streaming API.
It parses `is_final` and transcript fields.

### Transcript Synchronization
`TranscriptSynchronizer` compares the newest transcript to the previous one.
It:
- Extracts appended words when the text only grows
- Generates a correction command for mid-text changes

Corrections are conservative and intended for tail edits.

### Typing
`RealTimeTextInjector` uses `SendInput` and adds spacing between words.
It tracks:
- Last typed character
- Typed character count (for correction alignment)

### Key Settings
- `Streaming.Enabled`
- `Streaming.Provider`
- `Streaming.ChunkDurationMs`
- `Streaming.TypingDelayMs`
- `Streaming.ShowPartialResults`

### Known Limitations
- Corrections are applied only near the end of typed output.
- Streaming UI feedback is minimal (no overlay yet).

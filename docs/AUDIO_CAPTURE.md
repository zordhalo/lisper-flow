## Audio Capture

Audio capture is implemented via WASAPI (NAudio) and supports both batch and
streaming workflows.

### Components
- `WasapiCaptureManager`: low-level WASAPI capture
- `AudioCaptureService`: orchestration, resampling, VAD, buffering
- `VoiceActivityDetector`: Silero VAD (ONNX) with RMS fallback
- `RingBufferManager`: pre-roll buffer for VAD segments

### Sample Rates
The app targets 16 kHz mono audio. If the device captures at 48 kHz,
`AudioCaptureService` resamples to 16 kHz using simple linear interpolation.

### Batch Mode
- Audio samples are accumulated in `_pttBuffer`
- VAD segments can be emitted via `SpeechSegmentReady`
- `StopListeningAsync` returns a full sample array

### Streaming Mode
- `_streamingMode` enables chunk emission
- `StreamingChunkDurationMs` controls chunk size
- `AudioChunkReady` emits chunks to the streaming pipeline

### VAD Behavior
In streaming mode, VAD still emits speech start/end events but does not
close or emit speech segments.

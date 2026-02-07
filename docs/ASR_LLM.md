## ASR and LLM Providers

### Batch ASR
`IAsrProvider` defines the batch transcription contract.

#### OpenAI Whisper (Cloud)
`OpenAIWhisperProvider` posts WAV audio to OpenAI's `/audio/transcriptions`
endpoint. It returns a plain transcript string.

#### Whisper ONNX (Local)
`WhisperOnnxProvider` loads a local ONNX model but does not implement full
Whisper inference yet. It currently returns a failure result so the caller
falls back to cloud (if configured).

### Streaming ASR
`IStreamingAsrProvider` defines the streaming contract.

#### Azure Speech
`AzureSpeechStreamProvider` uses the Azure Speech SDK (push stream) and emits
partial and final results via events.

#### Deepgram
`DeepgramStreamProvider` connects to Deepgram’s WebSocket API and parses
partial/final transcripts from JSON responses.

### LLM Enhancement
`ILlmProvider` defines a simple chat completion interface.

#### OpenAI (Cloud)
`OpenAIProvider` uses OpenAI’s Chat Completions API. It is used for transcript
cleanup and punctuation.

#### Phi-3 ONNX (Local)
`Phi3OnnxProvider` loads a local ONNX model but does not implement full
generation. It returns the original text and a warning message.

### Prompt Construction
`PromptTemplateEngine` builds a system prompt that:
- removes filler words
- fixes punctuation/grammar
- preserves meaning
- applies tone based on application context

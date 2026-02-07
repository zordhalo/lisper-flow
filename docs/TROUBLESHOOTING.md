## Troubleshooting

### No text appears
- Ensure the target app has focus
- Check logs in `logs/` for injection errors
- Try batch mode to confirm ASR works

### Hotkey not working
- Another app may have registered the hotkey
- Update `Hotkey` settings in `appsettings.json`
- Restart the app after changing hotkey settings

### Streaming has high latency
- Verify network connectivity
- Try a different streaming provider
- Reduce `Streaming.ChunkDurationMs` to 80-100ms

### Clipboard injection fails
- Some apps block clipboard paste
- SendInput is used as fallback
- Anti-virus or security policies may block simulated input

### Azure Speech errors
- Check `AzureSpeech.SubscriptionKey` and `Region`
- Ensure Azure Speech resource is active
- Confirm `AzureSpeech.Language` is supported

### Deepgram errors
- Check `Deepgram.ApiKey`
- Confirm network access to `api.deepgram.com`

### Local models not working
Local ONNX inference is placeholder-only at this time. Use cloud providers
for actual transcription and enhancement until full inference is implemented.

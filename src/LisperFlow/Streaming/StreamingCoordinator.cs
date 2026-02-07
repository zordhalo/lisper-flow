using LisperFlow.Streaming.Models;
using Microsoft.Extensions.Logging;

namespace LisperFlow.Streaming;

public class StreamingCoordinator : IDisposable
{
    private readonly IAudioStreamProvider _audioStreamProvider;
    private readonly IStreamingAsrProvider _asrProvider;
    private readonly ILogger<StreamingCoordinator> _logger;
    private bool _isStreaming;
    
    public event EventHandler<PartialTranscriptEventArgs>? PartialTranscriptReceived;
    public event EventHandler<FinalTranscriptEventArgs>? FinalTranscriptReceived;
    
    public StreamingCoordinator(
        IAudioStreamProvider audioStreamProvider,
        IStreamingAsrProvider asrProvider,
        ILogger<StreamingCoordinator> logger)
    {
        _audioStreamProvider = audioStreamProvider;
        _asrProvider = asrProvider;
        _logger = logger;
        
        _audioStreamProvider.AudioChunkReady += OnAudioChunkReady;
        _asrProvider.PartialResultReceived += OnPartialResult;
        _asrProvider.FinalResultReceived += OnFinalResult;
    }
    
    public async Task StartStreamingAsync(CancellationToken cancellationToken = default)
    {
        if (_isStreaming)
        {
            throw new InvalidOperationException("Streaming session already active.");
        }
        
        await _asrProvider.ConnectAsync(cancellationToken);
        await _audioStreamProvider.StartStreamingAsync(cancellationToken);
        _isStreaming = true;
        
        _logger.LogInformation("Streaming coordinator started");
    }
    
    public async Task StopStreamingAsync(CancellationToken cancellationToken = default)
    {
        if (!_isStreaming) return;
        
        await _audioStreamProvider.StopStreamingAsync(cancellationToken);
        await _asrProvider.FinalizeAsync(cancellationToken);
        await _asrProvider.DisconnectAsync();
        _isStreaming = false;
        
        _logger.LogInformation("Streaming coordinator stopped");
    }
    
    private async void OnAudioChunkReady(object? sender, AudioChunkEventArgs e)
    {
        try
        {
            await _asrProvider.SendAudioAsync(e.Chunk);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send audio chunk to streaming ASR");
        }
    }
    
    private void OnPartialResult(object? sender, PartialTranscriptEventArgs e)
    {
        PartialTranscriptReceived?.Invoke(this, e);
    }
    
    private void OnFinalResult(object? sender, FinalTranscriptEventArgs e)
    {
        FinalTranscriptReceived?.Invoke(this, e);
    }
    
    public void Dispose()
    {
        _audioStreamProvider.AudioChunkReady -= OnAudioChunkReady;
        _asrProvider.PartialResultReceived -= OnPartialResult;
        _asrProvider.FinalResultReceived -= OnFinalResult;
    }
}

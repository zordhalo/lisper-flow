using LisperFlow.Streaming.Models;

namespace LisperFlow.Streaming;

public interface IStreamingAsrProvider : IDisposable
{
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task SendAudioAsync(AudioChunk chunk, CancellationToken cancellationToken = default);
    Task FinalizeAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    
    event EventHandler<PartialTranscriptEventArgs>? PartialResultReceived;
    event EventHandler<FinalTranscriptEventArgs>? FinalResultReceived;
    event EventHandler<StreamingErrorEventArgs>? ErrorOccurred;
}

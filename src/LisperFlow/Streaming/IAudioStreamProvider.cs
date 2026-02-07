using LisperFlow.Streaming.Models;

namespace LisperFlow.Streaming;

public interface IAudioStreamProvider
{
    event EventHandler<AudioChunkEventArgs>? AudioChunkReady;
    
    Task StartStreamingAsync(CancellationToken cancellationToken = default);
    Task StopStreamingAsync(CancellationToken cancellationToken = default);
}

public class AudioChunkEventArgs : EventArgs
{
    public AudioChunk Chunk { get; set; } = new();
}

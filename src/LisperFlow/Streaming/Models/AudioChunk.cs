namespace LisperFlow.Streaming.Models;

public class AudioChunk
{
    public float[] Samples { get; set; } = Array.Empty<float>();
    public int SampleRate { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

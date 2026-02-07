namespace LisperFlow.Streaming.Models;

public class PartialTranscriptEventArgs : EventArgs
{
    public string Text { get; set; } = "";
    public TimeSpan Offset { get; set; }
    public bool IsFinal { get; set; }
}

public class FinalTranscriptEventArgs : EventArgs
{
    public string Text { get; set; } = "";
    public double Confidence { get; set; }
    public TimeSpan Offset { get; set; }
    public TimeSpan Duration { get; set; }
}

public class StreamingErrorEventArgs : EventArgs
{
    public Exception Error { get; set; } = new Exception("Unknown streaming error");
    public string Message { get; set; } = "";
}

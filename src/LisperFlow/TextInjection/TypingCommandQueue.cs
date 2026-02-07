using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace LisperFlow.TextInjection;

public class TypingCommandQueue
{
    private Channel<ITypingCommand> _channel;
    private readonly ILogger<TypingCommandQueue> _logger;
    
    public TypingCommandQueue(ILogger<TypingCommandQueue> logger)
    {
        _logger = logger;
        _channel = CreateChannel();
    }
    
    public void EnqueueWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return;
        _channel.Writer.TryWrite(new TypeWordCommand { Word = word });
        _logger.LogTrace("Enqueued word: {Word}", word);
    }
    
    public void EnqueueCorrection(CorrectionCommand correction)
    {
        _channel.Writer.TryWrite(correction);
        _logger.LogDebug("Enqueued correction: {Old} -> {New}", correction.OldText, correction.NewText);
    }
    
    public async Task<ITypingCommand> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return await _channel.Reader.ReadAsync(cancellationToken);
    }
    
    public void Clear()
    {
        while (_channel.Reader.TryRead(out _)) { }
    }
    
    public void Complete()
    {
        _channel.Writer.TryComplete();
    }
    
    public void Reset()
    {
        Complete();
        _channel = CreateChannel();
    }
    
    private static Channel<ITypingCommand> CreateChannel()
    {
        return Channel.CreateUnbounded<ITypingCommand>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
    }
}

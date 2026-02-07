using LisperFlow.TextInjection;
using Microsoft.Extensions.Logging;

namespace LisperFlow.Streaming;

/// <summary>
/// Tracks which words have been typed and only emits new words to type.
/// Uses word-count tracking instead of character-level diffing to avoid
/// aggressive correction behaviour from fluctuating partial transcripts.
/// </summary>
public class TranscriptSynchronizer
{
    private readonly List<string> _typedWords = new();
    private readonly ILogger<TranscriptSynchronizer> _logger;
    
    public TranscriptSynchronizer(ILogger<TranscriptSynchronizer> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Process an incoming partial transcript. Only appends new words when
    /// the already-typed prefix still matches (fuzzy, ignoring case and
    /// trailing punctuation). If the ASR revised earlier words the partial
    /// is silently skipped—the final transcript will catch up.
    /// </summary>
    public TypingUpdate ProcessPartialTranscript(string newTranscript)
    {
        var update = new TypingUpdate();
        if (string.IsNullOrWhiteSpace(newTranscript)) return update;
        
        var newWords = newTranscript.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        // Nothing new to type
        if (newWords.Length <= _typedWords.Count) return update;
        
        // Verify the prefix we already typed still matches (fuzzy)
        for (int i = 0; i < _typedWords.Count; i++)
        {
            if (!FuzzyWordMatch(_typedWords[i], newWords[i]))
            {
                _logger.LogDebug(
                    "Partial diverged at word {Index}: typed '{Typed}' vs partial '{Partial}' — skipping until stabilised",
                    i, _typedWords[i], newWords[i]);
                return update; // Wait for the transcript to settle
            }
        }
        
        // Append new words
        for (int i = _typedWords.Count; i < newWords.Length; i++)
        {
            update.WordsToType.Add(newWords[i]);
            _typedWords.Add(newWords[i]);
        }
        
        _logger.LogTrace("Partial appended {Count} word(s), total typed: {Total}",
            update.WordsToType.Count, _typedWords.Count);
        
        return update;
    }
    
    /// <summary>
    /// Process a final (authoritative) transcript. Types any remaining
    /// words beyond what was already typed during partials, then resets
    /// internal state for the next utterance.
    /// </summary>
    public TypingUpdate ProcessFinalTranscript(string finalTranscript)
    {
        var update = new TypingUpdate();
        if (string.IsNullOrWhiteSpace(finalTranscript)) return update;
        
        var finalWords = finalTranscript.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        // Find how many leading words match what we already typed (fuzzy)
        int matchCount = 0;
        for (int i = 0; i < Math.Min(_typedWords.Count, finalWords.Length); i++)
        {
            if (FuzzyWordMatch(_typedWords[i], finalWords[i]))
                matchCount++;
            else
                break;
        }
        
        // Type everything after the matched prefix
        for (int i = matchCount; i < finalWords.Length; i++)
        {
            update.WordsToType.Add(finalWords[i]);
        }
        
        _logger.LogDebug(
            "Final transcript: {Total} words, {Matched} already typed, {New} new",
            finalWords.Length, matchCount, update.WordsToType.Count);
        
        return update;
    }
    
    public void Reset()
    {
        _typedWords.Clear();
    }
    
    /// <summary>
    /// Fuzzy word comparison that ignores case and trailing punctuation,
    /// so "world" matches "world," and "Hello" matches "hello".
    /// </summary>
    private static bool FuzzyWordMatch(string a, string b)
    {
        return NormalizeWord(a).Equals(NormalizeWord(b), StringComparison.OrdinalIgnoreCase);
    }
    
    private static ReadOnlySpan<char> PunctuationChars => ".,:;!?\"')]-".AsSpan();
    
    private static string NormalizeWord(string word)
    {
        return word.TrimEnd('.', ',', ':', ';', '!', '?', '"', '\'', ')', ']', '-');
    }
}

public class TypingUpdate
{
    public List<string> WordsToType { get; } = new();
}

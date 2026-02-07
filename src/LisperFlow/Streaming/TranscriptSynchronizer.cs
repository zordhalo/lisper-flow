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
    
    // Track last partial to detect stabilization
    private string _lastPartial = "";
    private int _partialStableCount = 0;
    private const int MinStableCountBeforeTyping = 1; // Require 1 repeat before typing new words
    
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
        
        // Track stability - only type if the partial has stabilized
        if (newTranscript == _lastPartial)
        {
            _partialStableCount++;
        }
        else
        {
            _partialStableCount = 0;
            _lastPartial = newTranscript;
        }
        
        // Don't type from first partial - wait for at least one confirmation
        // This prevents typing "Basic" when "Basically" is coming
        if (_typedWords.Count == 0 && _partialStableCount < MinStableCountBeforeTyping)
        {
            _logger.LogTrace("Waiting for partial to stabilize before typing first word");
            return update;
        }
        
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
        
        // For subsequent words, also wait for some stability before typing
        // Only type words that have appeared in at least 1 consecutive partial
        int wordsToConsider = newWords.Length;
        if (_partialStableCount < MinStableCountBeforeTyping && newWords.Length > _typedWords.Count + 1)
        {
            // Only type up to one new word if not yet stable
            wordsToConsider = _typedWords.Count + 1;
        }
        
        // Append new words
        for (int i = _typedWords.Count; i < wordsToConsider; i++)
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
        
        // If there's a mismatch at the beginning, we need to correct what was typed
        if (matchCount < _typedWords.Count && _typedWords.Count > 0)
        {
            // Calculate how many characters to delete (all mismatched typed words + spaces)
            int charsToDelete = 0;
            for (int i = matchCount; i < _typedWords.Count; i++)
            {
                charsToDelete += _typedWords[i].Length + 1; // +1 for space
            }
            update.CharsToDelete = charsToDelete;
            
            _logger.LogDebug(
                "Final transcript correction: deleting {Chars} chars from word {Start} to {End}",
                charsToDelete, matchCount, _typedWords.Count - 1);
            
            // Type the correct words from where mismatch started
            for (int i = matchCount; i < finalWords.Length; i++)
            {
                update.WordsToType.Add(finalWords[i]);
            }
        }
        else
        {
            // No mismatch, just type remaining words
            for (int i = matchCount; i < finalWords.Length; i++)
            {
                update.WordsToType.Add(finalWords[i]);
            }
        }
        
        _logger.LogDebug(
            "Final transcript: {Total} words, {Matched} already typed, {New} new, {Delete} chars to delete",
            finalWords.Length, matchCount, update.WordsToType.Count, update.CharsToDelete);
        
        return update;
    }
    
    public void Reset()
    {
        _typedWords.Clear();
        _lastPartial = "";
        _partialStableCount = 0;
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
    public int CharsToDelete { get; set; } = 0;
}

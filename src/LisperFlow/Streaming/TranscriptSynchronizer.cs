using LisperFlow.TextInjection;
using Microsoft.Extensions.Logging;

namespace LisperFlow.Streaming;

public class TranscriptSynchronizer
{
    private string _previousTranscript = "";
    private readonly ILogger<TranscriptSynchronizer> _logger;
    
    public TranscriptSynchronizer(ILogger<TranscriptSynchronizer> logger)
    {
        _logger = logger;
    }
    
    public TypingUpdate ProcessPartialTranscript(string newTranscript)
    {
        var update = new TypingUpdate();
        
        if (string.IsNullOrWhiteSpace(newTranscript))
        {
            _previousTranscript = newTranscript;
            return update;
        }
        
        if (string.IsNullOrEmpty(_previousTranscript))
        {
            update.WordsToType.AddRange(SplitIntoWords(newTranscript));
            _previousTranscript = newTranscript;
            return update;
        }
        
        if (newTranscript == _previousTranscript)
        {
            return update;
        }
        
        int prefix = FindCommonPrefixLength(_previousTranscript, newTranscript);
        int suffix = FindCommonSuffixLength(_previousTranscript, newTranscript, prefix);
        
        int oldChangedLength = _previousTranscript.Length - prefix - suffix;
        int newChangedLength = newTranscript.Length - prefix - suffix;
        
        string oldChanged = oldChangedLength > 0
            ? _previousTranscript.Substring(prefix, oldChangedLength)
            : "";
        string newChanged = newChangedLength > 0
            ? newTranscript.Substring(prefix, newChangedLength)
            : "";
        
        if (oldChangedLength == 0 && newChangedLength > 0)
        {
            // Pure append
            update.WordsToType.AddRange(SplitIntoWords(newChanged.TrimStart()));
        }
        else
        {
            // Correction or replacement
            update.Correction = new CorrectionCommand
            {
                Position = prefix,
                CharactersToDelete = oldChangedLength,
                OldText = oldChanged,
                NewText = newChanged
            };
            
            _logger.LogDebug("Transcript correction detected: '{Old}' -> '{New}'", oldChanged, newChanged);
        }
        
        _previousTranscript = newTranscript;
        return update;
    }
    
    public void Reset()
    {
        _previousTranscript = "";
    }
    
    private static List<string> SplitIntoWords(string text)
    {
        return text
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }
    
    private static int FindCommonPrefixLength(string s1, string s2)
    {
        int min = Math.Min(s1.Length, s2.Length);
        for (int i = 0; i < min; i++)
        {
            if (s1[i] != s2[i])
            {
                return i;
            }
        }
        return min;
    }
    
    private static int FindCommonSuffixLength(string s1, string s2, int prefixLength)
    {
        int s1Index = s1.Length - 1;
        int s2Index = s2.Length - 1;
        int count = 0;
        
        while (s1Index >= prefixLength && s2Index >= prefixLength)
        {
            if (s1[s1Index] != s2[s2Index])
            {
                break;
            }
            
            count++;
            s1Index--;
            s2Index--;
        }
        
        return count;
    }
}

public class TypingUpdate
{
    public List<string> WordsToType { get; } = new();
    public CorrectionCommand? Correction { get; set; }
}

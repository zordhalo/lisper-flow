using System.Text;
using LisperFlow.Context;

namespace LisperFlow.LLM;

/// <summary>
/// Builds prompts for LLM enhancement based on context
/// </summary>
public class PromptTemplateEngine
{
    /// <summary>
    /// Build the system prompt for transcript enhancement
    /// </summary>
    public string BuildSystemPrompt(EnhancementContext context)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("You are an expert transcription assistant. Your job is to clean up raw speech-to-text transcripts.");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("1. Remove filler words (um, uh, like, you know, so, etc.)");
        sb.AppendLine("2. Add proper punctuation (periods, commas, question marks)");
        sb.AppendLine("3. Fix obvious grammar errors");
        sb.AppendLine("4. Correct commonly misheard words");
        sb.AppendLine("5. Preserve the speaker's original meaning and intent exactly");
        sb.AppendLine("6. Do NOT add information that wasn't in the original speech");
        sb.AppendLine("7. Do NOT summarize or paraphrase—keep ALL content");
        sb.AppendLine("8. Format any lists or bullet points properly if detected");
        sb.AppendLine();
        
        // Add tone guidance
        if (context.TonePreference != ToneType.Default)
        {
            sb.AppendLine($"Tone: {GetToneGuidance(context.TonePreference)}");
            sb.AppendLine();
        }
        
        // Add application context
        if (!string.IsNullOrEmpty(context.ApplicationName))
        {
            var appGuidance = GetApplicationGuidance(context.ApplicationName);
            if (!string.IsNullOrEmpty(appGuidance))
            {
                sb.AppendLine($"Context: The user is typing in {context.ApplicationName}.");
                sb.AppendLine(appGuidance);
                sb.AppendLine();
            }
        }
        
        // Add personal dictionary if available
        if (context.PersonalDictionary?.Count > 0)
        {
            sb.AppendLine("Personal dictionary (use these exact spellings when you hear similar words):");
            foreach (var entry in context.PersonalDictionary.Take(15))
            {
                sb.AppendLine($"- {entry}");
            }
            sb.AppendLine();
        }
        
        sb.AppendLine("IMPORTANT: Output ONLY the cleaned transcript. No explanations, no quotes, no prefix text.");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Build the user prompt with the raw transcript
    /// </summary>
    public string BuildUserPrompt(string rawTranscript)
    {
        return $"Clean this transcript:\n\n{rawTranscript}";
    }
    
    private static string GetToneGuidance(ToneType tone)
    {
        return tone switch
        {
            ToneType.Professional => "Use formal, polished language suitable for business communication.",
            ToneType.Casual => "Keep it conversational and friendly, suitable for messaging apps.",
            ToneType.Technical => "Preserve technical terms precisely. Format code-related content appropriately.",
            ToneType.Creative => "Maintain the speaker's natural voice and expressive style.",
            _ => ""
        };
    }
    
    private static string GetApplicationGuidance(string appName)
    {
        var lowerName = appName.ToLowerInvariant();
        
        return lowerName switch
        {
            "gmail" or "outlook" or "thunderbird" => 
                "Format as professional email. Use complete sentences.",
            
            "slack" or "teams" or "discord" => 
                "Format as casual message. Short paragraphs are fine.",
            
            "vscode" or "code" or "cursor" or "visualstudio" or "devenv" => 
                "May contain code syntax or technical terms. Preserve them exactly.",
            
            "notion" or "obsidian" or "onenote" => 
                "Format as structured notes. Use headers and bullets where appropriate.",
            
            "chrome" or "firefox" or "edge" or "msedge" => 
                "", // Could be any web context
            
            "winword" or "word" => 
                "Format as formal document with proper paragraph structure.",
            
            _ => ""
        };
    }
}

/// <summary>
/// Context information for LLM enhancement
/// </summary>
public class EnhancementContext
{
    public string RawTranscript { get; set; } = "";
    public string ApplicationName { get; set; } = "";
    public string WindowTitle { get; set; } = "";
    public ToneType TonePreference { get; set; } = ToneType.Default;
    public List<string>? PersonalDictionary { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

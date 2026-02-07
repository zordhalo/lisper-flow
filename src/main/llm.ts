import OpenAI from 'openai';
import { configStore } from '../shared/config';

const CLEANUP_SYSTEM_PROMPT = `You are a transcript cleanup assistant. Your job is to clean up speech-to-text transcripts.

Rules:
1. Remove filler words (um, uh, like, you know, etc.) unless they're essential to meaning
2. Fix obvious grammatical errors
3. Ensure proper punctuation and capitalization
4. Preserve the speaker's original meaning and tone
5. Keep the text natural and conversational
6. Do not add information that wasn't in the original
7. Do not change technical terms or proper nouns
8. Return ONLY the cleaned transcript, no explanations

If the input is empty or just noise, return an empty string.`;

export async function cleanTranscript(rawText: string): Promise<string> {
  // Check if cleanup is enabled
  if (!configStore.get('cleanupEnabled')) {
    return rawText;
  }

  // Check for API key
  const apiKey = configStore.get('llmApiKey');
  if (!apiKey) {
    console.warn('LLM API key not configured, returning raw transcript');
    return rawText;
  }

  // Don't process empty text
  if (!rawText || rawText.trim().length === 0) {
    return rawText;
  }

  try {
    const openai = new OpenAI({ apiKey });

    const response = await openai.chat.completions.create({
      model: 'gpt-4o-mini',
      messages: [
        { role: 'system', content: CLEANUP_SYSTEM_PROMPT },
        { role: 'user', content: rawText },
      ],
      temperature: 0.3,
      max_tokens: 1000,
    });

    const cleanedText = response.choices[0]?.message?.content?.trim();

    if (!cleanedText) {
      console.warn('LLM returned empty response, using raw transcript');
      return rawText;
    }

    return cleanedText;
  } catch (error) {
    console.error('LLM cleanup failed:', error);
    // Graceful fallback to raw text
    return rawText;
  }
}

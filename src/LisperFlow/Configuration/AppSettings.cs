using System.Collections.Generic;

namespace LisperFlow.Configuration;

/// <summary>
/// Application settings loaded from appsettings.json
/// </summary>
public class AppSettings
{
    public AudioSettings Audio { get; set; } = new();
    public HotkeySettings Hotkey { get; set; } = new();
    public AsrSettings Asr { get; set; } = new();
    public LlmSettings Llm { get; set; } = new();
    public StreamingSettings Streaming { get; set; } = new();
    public AzureSpeechSettings AzureSpeech { get; set; } = new();
    public DeepgramSettings Deepgram { get; set; } = new();
    public PrivacySettings Privacy { get; set; } = new();
}

public class AudioSettings
{
    /// <summary>
    /// Device ID for audio capture. Null uses default device.
    /// </summary>
    public string? DeviceId { get; set; }
    
    /// <summary>
    /// Sample rate in Hz (default: 48000 for streaming accuracy)
    /// </summary>
    public int SampleRate { get; set; } = 48000;
    
    /// <summary>
    /// Voice Activity Detection threshold (0.0 to 1.0)
    /// </summary>
    public float VadThreshold { get; set; } = 0.4f;
    
    /// <summary>
    /// Ring buffer capacity in seconds
    /// </summary>
    public int BufferSeconds { get; set; } = 10;
    
    /// <summary>
    /// Pre-roll audio before VAD triggers (milliseconds)
    /// </summary>
    public int PreRollMs { get; set; } = 500;
}

public class HotkeySettings
{
    /// <summary>
    /// Whether Control modifier is required
    /// </summary>
    public bool UseControl { get; set; } = true;
    
    /// <summary>
    /// Whether Alt modifier is required
    /// </summary>
    public bool UseAlt { get; set; } = false;
    
    /// <summary>
    /// Whether Shift modifier is required
    /// </summary>
    public bool UseShift { get; set; } = false;
    
    /// <summary>
    /// Whether Windows key modifier is required
    /// </summary>
    public bool UseWin { get; set; } = false;
    
    /// <summary>
    /// Virtual key code for the main key (default: Space = 0x20)
    /// </summary>
    public int VirtualKeyCode { get; set; } = 0x20; // Space
}

public class AsrSettings
{
    /// <summary>
    /// Provider: "Local" or "Cloud"
    /// </summary>
    public string Provider { get; set; } = "Local";
    
    /// <summary>
    /// Path to Whisper ONNX model directory
    /// </summary>
    public string LocalModelPath { get; set; } = "models/whisper-small-onnx";
    
    /// <summary>
    /// OpenAI API key for cloud ASR
    /// </summary>
    public string? OpenAiApiKey { get; set; }
    
    /// <summary>
    /// Language code (e.g., "en" for English)
    /// </summary>
    public string Language { get; set; } = "en";
    
    /// <summary>
    /// Use GPU acceleration via DirectML
    /// </summary>
    public bool UseGpu { get; set; } = true;
}

public class LlmSettings
{
    /// <summary>
    /// Provider: "Local" or "Cloud"
    /// </summary>
    public string Provider { get; set; } = "Local";
    
    /// <summary>
    /// Path to LLM ONNX model directory
    /// </summary>
    public string LocalModelPath { get; set; } = "models/phi3-mini-onnx";
    
    /// <summary>
    /// OpenAI API key for cloud LLM (can share with ASR)
    /// </summary>
    public string? OpenAiApiKey { get; set; }
    
    /// <summary>
    /// Cloud model name (e.g., "gpt-4o-mini")
    /// </summary>
    public string CloudModel { get; set; } = "gpt-4o-mini";
    
    /// <summary>
    /// Enable transcript enhancement (filler removal, punctuation)
    /// </summary>
    public bool EnableEnhancement { get; set; } = true;
    
    /// <summary>
    /// Use GPU acceleration via DirectML
    /// </summary>
    public bool UseGpu { get; set; } = true;
}

public class StreamingSettings
{
    /// <summary>
    /// Enable streaming dictation mode
    /// </summary>
    public bool Enabled { get; set; } = false;
    
    /// <summary>
    /// Streaming ASR provider (e.g., "AzureSpeech", "Deepgram")
    /// </summary>
    public string Provider { get; set; } = "AzureSpeech";
    
    /// <summary>
    /// Audio chunk duration for streaming (milliseconds)
    /// </summary>
    public int ChunkDurationMs { get; set; } = 100;
    
    /// <summary>
    /// Per-word typing delay (milliseconds)
    /// </summary>
    public int TypingDelayMs { get; set; } = 25;
    
    /// <summary>
    /// Whether partial results should be emitted/typed
    /// </summary>
    public bool ShowPartialResults { get; set; } = true;
    
    /// <summary>
    /// When to apply LLM enhancement: "AfterFinalization" or "Disabled"
    /// </summary>
    public string ApplyLlmEnhancement { get; set; } = "AfterFinalization";
}

public class AzureSpeechSettings
{
    /// <summary>
    /// Azure Speech subscription key
    /// </summary>
    public string? SubscriptionKey { get; set; }
    
    /// <summary>
    /// Azure Speech region (e.g., "eastus")
    /// </summary>
    public string Region { get; set; } = "eastus";
    
    /// <summary>
    /// Recognition language (e.g., "en-US")
    /// </summary>
    public string Language { get; set; } = "en-US";
    
    /// <summary>
    /// Whether to enable profanity filtering
    /// </summary>
    public bool EnableProfanityFilter { get; set; } = false;
}

public class DeepgramSettings
{
    /// <summary>
    /// Deepgram API key
    /// </summary>
    public string? ApiKey { get; set; }
    
    /// <summary>
    /// Recognition language (e.g., "en")
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// Model name (e.g., "nova-3")
    /// </summary>
    public string Model { get; set; } = "nova-3";

    /// <summary>
    /// Pricing tier (e.g., "enhanced" or "base")
    /// </summary>
    public string Tier { get; set; } = "enhanced";

    /// <summary>
    /// Endpointing in milliseconds (0 = provider default)
    /// </summary>
    public int Endpointing { get; set; } = 300;

    /// <summary>
    /// Include filler words in transcripts
    /// </summary>
    public bool FillerWords { get; set; } = true;

    /// <summary>
    /// Convert spoken numerals to digits
    /// </summary>
    public bool Numerals { get; set; } = true;

    /// <summary>
    /// Enable speaker diarization
    /// </summary>
    public bool Diarize { get; set; } = false;

    /// <summary>
    /// Enable profanity filtering
    /// </summary>
    public bool ProfanityFilter { get; set; } = false;

    /// <summary>
    /// Key terms to boost recognition
    /// </summary>
    public List<string> Keyterms { get; set; } = new();
}

public class PrivacySettings
{
    /// <summary>
    /// Store transcripts locally for improvement
    /// </summary>
    public bool StoreTranscripts { get; set; } = false;
    
    /// <summary>
    /// Send usage analytics (anonymized)
    /// </summary>
    public bool SendAnalytics { get; set; } = false;
    
    /// <summary>
    /// Days to retain local data (0 = indefinite)
    /// </summary>
    public int DataRetentionDays { get; set; } = 30;
}

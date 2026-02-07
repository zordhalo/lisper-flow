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
    public PrivacySettings Privacy { get; set; } = new();
}

public class AudioSettings
{
    /// <summary>
    /// Device ID for audio capture. Null uses default device.
    /// </summary>
    public string? DeviceId { get; set; }
    
    /// <summary>
    /// Sample rate in Hz (default: 16000 for Whisper)
    /// </summary>
    public int SampleRate { get; set; } = 16000;
    
    /// <summary>
    /// Voice Activity Detection threshold (0.0 to 1.0)
    /// </summary>
    public float VadThreshold { get; set; } = 0.5f;
    
    /// <summary>
    /// Ring buffer capacity in seconds
    /// </summary>
    public int BufferSeconds { get; set; } = 10;
    
    /// <summary>
    /// Pre-roll audio before VAD triggers (milliseconds)
    /// </summary>
    public int PreRollMs { get; set; } = 300;
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
    public bool UseWin { get; set; } = true;
    
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

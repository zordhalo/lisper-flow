using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;

namespace LisperFlow.ASR;

/// <summary>
/// Cloud ASR provider using OpenAI Whisper API
/// </summary>
public class OpenAIWhisperProvider : IAsrProvider, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<OpenAIWhisperProvider> _logger;
    
    private const string WHISPER_API_URL = "https://api.openai.com/v1/audio/transcriptions";
    
    public string Name => "OpenAI-Whisper";
    
    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);
    
    public OpenAIWhisperProvider(string apiKey, ILogger<OpenAIWhisperProvider> logger)
    {
        _apiKey = apiKey;
        _logger = logger;
        
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        
        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", _apiKey);
        }
    }
    
    public async Task<AsrResult> TranscribeAsync(
        float[] audioSamples,
        int sampleRate = 16000,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return AsrResult.Failed("OpenAI API key not configured");
        }
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Convert float samples to WAV file in memory
            byte[] wavData = ConvertToWav(audioSamples, sampleRate);
            
            _logger.LogDebug(
                "Sending {Duration:F2}s audio to OpenAI Whisper ({Size} bytes)",
                audioSamples.Length / (float)sampleRate,
                wavData.Length);
            
            // Create multipart form data
            using var content = new MultipartFormDataContent();
            using var audioContent = new ByteArrayContent(wavData);
            
            audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            content.Add(audioContent, "file", "audio.wav");
            content.Add(new StringContent("whisper-1"), "model");
            content.Add(new StringContent("en"), "language");
            content.Add(new StringContent("text"), "response_format");
            
            // Send request
            var response = await _httpClient.PostAsync(WHISPER_API_URL, content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "OpenAI API error: {StatusCode} - {Body}",
                    response.StatusCode,
                    errorBody);
                
                return AsrResult.Failed($"API error: {response.StatusCode}");
            }
            
            var transcript = await response.Content.ReadAsStringAsync(cancellationToken);
            transcript = transcript.Trim();
            
            stopwatch.Stop();
            
            _logger.LogInformation(
                "Transcription completed in {Ms}ms: {Preview}...",
                stopwatch.ElapsedMilliseconds,
                transcript.Length > 50 ? transcript[..50] : transcript);
            
            return new AsrResult
            {
                Transcript = transcript,
                Confidence = 1.0f,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                Provider = Name,
                Success = true
            };
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Transcription cancelled");
            return AsrResult.Failed("Transcription cancelled");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed");
            return AsrResult.Failed($"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transcription failed");
            return AsrResult.Failed(ex.Message);
        }
    }
    
    private static byte[] ConvertToWav(float[] samples, int sampleRate)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        
        int bytesPerSample = 2; // 16-bit
        int channels = 1;      // Mono
        int dataSize = samples.Length * bytesPerSample;
        
        // Write WAV header
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize); // File size - 8
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        
        // fmt chunk
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16); // Chunk size
        writer.Write((short)1); // PCM format
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bytesPerSample); // Byte rate
        writer.Write((short)(channels * bytesPerSample)); // Block align
        writer.Write((short)(bytesPerSample * 8)); // Bits per sample
        
        // data chunk
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);
        
        // Write samples
        foreach (float sample in samples)
        {
            // Clamp to [-1, 1] and convert to 16-bit
            float clamped = Math.Clamp(sample, -1f, 1f);
            short sampleValue = (short)(clamped * short.MaxValue);
            writer.Write(sampleValue);
        }
        
        return ms.ToArray();
    }
    
    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

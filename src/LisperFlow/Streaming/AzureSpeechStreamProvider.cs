using System.Text.Json;
using LisperFlow.Streaming.Models;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Logging;

namespace LisperFlow.Streaming;

public class AzureSpeechStreamProvider : IStreamingAsrProvider
{
    private readonly string _subscriptionKey;
    private readonly string _region;
    private readonly string _language;
    private readonly bool _profanityFilterEnabled;
    private readonly ILogger<AzureSpeechStreamProvider> _logger;
    
    private PushAudioInputStream? _audioStream;
    private SpeechRecognizer? _recognizer;
    
    public event EventHandler<PartialTranscriptEventArgs>? PartialResultReceived;
    public event EventHandler<FinalTranscriptEventArgs>? FinalResultReceived;
    public event EventHandler<StreamingErrorEventArgs>? ErrorOccurred;
    
    public AzureSpeechStreamProvider(
        string subscriptionKey,
        string region,
        string language,
        bool profanityFilterEnabled,
        ILogger<AzureSpeechStreamProvider> logger)
    {
        _subscriptionKey = subscriptionKey;
        _region = region;
        _language = language;
        _profanityFilterEnabled = profanityFilterEnabled;
        _logger = logger;
    }
    
    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_subscriptionKey))
        {
            throw new InvalidOperationException("Azure Speech subscription key is not configured.");
        }
        
        var config = SpeechConfig.FromSubscription(_subscriptionKey, _region);
        config.SpeechRecognitionLanguage = _language;
        config.OutputFormat = OutputFormat.Detailed;
        
        if (_profanityFilterEnabled)
        {
            config.SetProfanity(ProfanityOption.Removed);
        }
        
        _audioStream = AudioInputStream.CreatePushStream(
            AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1));
        var audioConfig = AudioConfig.FromStreamInput(_audioStream);
        
        _recognizer = new SpeechRecognizer(config, audioConfig);
        _recognizer.Recognizing += OnRecognizing;
        _recognizer.Recognized += OnRecognized;
        _recognizer.Canceled += OnCanceled;
        _recognizer.SessionStopped += OnSessionStopped;
        
        return _recognizer.StartContinuousRecognitionAsync();
    }
    
    public Task SendAudioAsync(AudioChunk chunk, CancellationToken cancellationToken = default)
    {
        if (_audioStream == null)
        {
            throw new InvalidOperationException("Azure Speech stream not initialized.");
        }
        
        var pcmBytes = ConvertToPcm16(chunk.Samples);
        _audioStream.Write(pcmBytes);
        return Task.CompletedTask;
    }
    
    public Task FinalizeAsync(CancellationToken cancellationToken = default)
    {
        _audioStream?.Close();
        return Task.CompletedTask;
    }
    
    public async Task DisconnectAsync()
    {
        if (_recognizer != null)
        {
            await _recognizer.StopContinuousRecognitionAsync();
            _recognizer.Recognizing -= OnRecognizing;
            _recognizer.Recognized -= OnRecognized;
            _recognizer.Canceled -= OnCanceled;
            _recognizer.SessionStopped -= OnSessionStopped;
            _recognizer.Dispose();
            _recognizer = null;
        }
        
        _audioStream?.Dispose();
        _audioStream = null;
    }
    
    private void OnRecognizing(object? sender, SpeechRecognitionEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Result.Text)) return;
        
        PartialResultReceived?.Invoke(this, new PartialTranscriptEventArgs
        {
            Text = e.Result.Text,
            Offset = TimeSpan.FromTicks(e.Result.OffsetInTicks),
            IsFinal = false
        });
    }
    
    private void OnRecognized(object? sender, SpeechRecognitionEventArgs e)
    {
        if (e.Result.Reason != ResultReason.RecognizedSpeech || string.IsNullOrWhiteSpace(e.Result.Text))
        {
            return;
        }
        
        FinalResultReceived?.Invoke(this, new FinalTranscriptEventArgs
        {
            Text = e.Result.Text,
            Confidence = TryExtractConfidence(e.Result),
            Offset = TimeSpan.FromTicks(e.Result.OffsetInTicks),
            Duration = TimeSpan.FromTicks(e.Result.Duration.Ticks)
        });
    }
    
    private void OnCanceled(object? sender, SpeechRecognitionCanceledEventArgs e)
    {
        var ex = new Exception($"Azure Speech canceled: {e.Reason} - {e.ErrorDetails}");
        _logger.LogError(ex, "Azure Speech canceled");
        ErrorOccurred?.Invoke(this, new StreamingErrorEventArgs
        {
            Error = ex,
            Message = e.ErrorDetails ?? "Recognition canceled"
        });
    }
    
    private void OnSessionStopped(object? sender, SessionEventArgs e)
    {
        _logger.LogInformation("Azure Speech session stopped");
    }
    
    private static byte[] ConvertToPcm16(float[] samples)
    {
        var pcm = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            float clamped = Math.Clamp(samples[i], -1f, 1f);
            short value = (short)(clamped * short.MaxValue);
            BitConverter.GetBytes(value).CopyTo(pcm, i * 2);
        }
        return pcm;
    }
    
    private static double TryExtractConfidence(SpeechRecognitionResult result)
    {
        try
        {
            string? json = result.Properties.GetProperty(PropertyId.SpeechServiceResponse_JsonResult);
            if (string.IsNullOrWhiteSpace(json)) return 1.0;
            
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("NBest", out var nbest) &&
                nbest.ValueKind == JsonValueKind.Array &&
                nbest.GetArrayLength() > 0 &&
                nbest[0].TryGetProperty("Confidence", out var confidence))
            {
                return confidence.GetDouble();
            }
        }
        catch
        {
            // Ignore confidence parse errors
        }
        
        return 1.0;
    }
    
    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
    }
}

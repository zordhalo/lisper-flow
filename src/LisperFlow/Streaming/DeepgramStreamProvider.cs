using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using LisperFlow.Streaming.Models;
using Microsoft.Extensions.Logging;

namespace LisperFlow.Streaming;

public class DeepgramStreamProvider : IStreamingAsrProvider
{
    private readonly string _apiKey;
    private readonly string _language;
    private readonly ILogger<DeepgramStreamProvider> _logger;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;
    
    public event EventHandler<PartialTranscriptEventArgs>? PartialResultReceived;
    public event EventHandler<FinalTranscriptEventArgs>? FinalResultReceived;
    public event EventHandler<StreamingErrorEventArgs>? ErrorOccurred;
    
    public DeepgramStreamProvider(string apiKey, string language, ILogger<DeepgramStreamProvider> logger)
    {
        _apiKey = apiKey;
        _language = language;
        _logger = logger;
    }
    
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("Deepgram API key is not configured.");
        }
        
        _webSocket = new ClientWebSocket();
        _webSocket.Options.SetRequestHeader("Authorization", $"Token {_apiKey}");
        
        var uri = new Uri(
            $"wss://api.deepgram.com/v1/listen?encoding=linear16&sample_rate=16000&channels=1" +
            $"&language={_language}&interim_results=true&punctuate=true&smart_format=true");
        
        await _webSocket.ConnectAsync(uri, cancellationToken);
        _receiveCts = new CancellationTokenSource();
        _receiveTask = ReceiveLoopAsync(_receiveCts.Token);
        
        _logger.LogInformation("Connected to Deepgram streaming ASR");
    }
    
    public async Task SendAudioAsync(AudioChunk chunk, CancellationToken cancellationToken = default)
    {
        if (_webSocket == null || _webSocket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("Deepgram WebSocket not connected.");
        }
        
        var pcmBytes = ConvertToPcm16(chunk.Samples);
        await _webSocket.SendAsync(
            new ArraySegment<byte>(pcmBytes),
            WebSocketMessageType.Binary,
            endOfMessage: true,
            cancellationToken);
    }
    
    public async Task FinalizeAsync(CancellationToken cancellationToken = default)
    {
        if (_webSocket == null || _webSocket.State != WebSocketState.Open) return;
        
        var closeMessage = Encoding.UTF8.GetBytes("{\"type\":\"CloseStream\"}");
        await _webSocket.SendAsync(
            new ArraySegment<byte>(closeMessage),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken);
    }
    
    public async Task DisconnectAsync()
    {
        _receiveCts?.Cancel();
        
        if (_receiveTask != null)
        {
            await _receiveTask;
        }
        
        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            await _webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Client disconnect",
                CancellationToken.None);
        }
        
        _webSocket?.Dispose();
        _webSocket = null;
    }
    
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var messageBuilder = new StringBuilder();
        
        while (!cancellationToken.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
        {
            try
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
                
                messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                
                if (result.EndOfMessage)
                {
                    var message = messageBuilder.ToString();
                    messageBuilder.Clear();
                    ProcessMessage(message);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deepgram receive loop error");
                ErrorOccurred?.Invoke(this, new StreamingErrorEventArgs
                {
                    Error = ex,
                    Message = "Deepgram receive loop error"
                });
                break;
            }
        }
    }
    
    private void ProcessMessage(string message)
    {
        try
        {
            using var doc = JsonDocument.Parse(message);
            if (!doc.RootElement.TryGetProperty("channel", out var channel)) return;
            if (!channel.TryGetProperty("alternatives", out var alternatives)) return;
            
            var alt = alternatives.EnumerateArray().FirstOrDefault();
            if (!alt.TryGetProperty("transcript", out var transcriptProp)) return;
            
            string transcript = transcriptProp.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(transcript)) return;
            
            bool isFinal = doc.RootElement.TryGetProperty("is_final", out var isFinalProp) &&
                           isFinalProp.GetBoolean();
            
            if (isFinal)
            {
                _logger.LogDebug("Deepgram final transcript: {Text}", transcript);
                FinalResultReceived?.Invoke(this, new FinalTranscriptEventArgs
                {
                    Text = transcript,
                    Confidence = 1.0,
                    Offset = TimeSpan.Zero,
                    Duration = TimeSpan.Zero
                });
            }
            else
            {
                _logger.LogDebug("Deepgram partial transcript: {Text}", transcript);
                PartialResultReceived?.Invoke(this, new PartialTranscriptEventArgs
                {
                    Text = transcript,
                    Offset = TimeSpan.Zero,
                    IsFinal = false
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Deepgram message: {Message}", message);
        }
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
    
    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
    }
}

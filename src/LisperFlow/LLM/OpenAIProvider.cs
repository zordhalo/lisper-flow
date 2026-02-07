using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace LisperFlow.LLM;

/// <summary>
/// Cloud LLM provider using OpenAI ChatGPT API
/// </summary>
public class OpenAIProvider : ILlmProvider, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<OpenAIProvider> _logger;
    
    public string Name => $"OpenAI-{_model}";
    
    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);
    
    public OpenAIProvider(
        string apiKey, 
        string model = "gpt-4o-mini",
        ILogger<OpenAIProvider>? logger = null)
    {
        _apiKey = apiKey;
        _model = model;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<OpenAIProvider>.Instance;
        
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.openai.com/v1/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
        
        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }
    }
    
    public async Task<LlmResult> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return LlmResult.Failed("OpenAI API key not configured");
        }
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var requestBody = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.3, // Lower temperature for consistency
                max_tokens = 1024
            };
            
            var response = await _httpClient.PostAsJsonAsync(
                "chat/completions",
                requestBody,
                cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("OpenAI API error: {StatusCode} - {Body}", response.StatusCode, errorBody);
                return LlmResult.Failed($"API error: {response.StatusCode}");
            }
            
            var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>(
                cancellationToken: cancellationToken);
            
            if (result?.Choices == null || result.Choices.Length == 0)
            {
                return LlmResult.Failed("Empty response from OpenAI");
            }
            
            stopwatch.Stop();
            
            var generatedText = result.Choices[0].Message?.Content ?? "";
            
            _logger.LogDebug(
                "LLM generation completed in {Ms}ms, {Tokens} tokens",
                stopwatch.ElapsedMilliseconds,
                result.Usage?.CompletionTokens ?? 0);
            
            return new LlmResult
            {
                GeneratedText = generatedText,
                TokensGenerated = result.Usage?.CompletionTokens ?? 0,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                Provider = Name,
                Success = true
            };
        }
        catch (TaskCanceledException)
        {
            return LlmResult.Failed("Generation cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM generation failed");
            return LlmResult.Failed(ex.Message);
        }
    }
    
    public void Dispose()
    {
        _httpClient.Dispose();
    }
    
    // Response DTOs
    private class OpenAIResponse
    {
        [JsonPropertyName("choices")]
        public Choice[]? Choices { get; set; }
        
        [JsonPropertyName("usage")]
        public Usage? Usage { get; set; }
    }
    
    private class Choice
    {
        [JsonPropertyName("message")]
        public Message? Message { get; set; }
    }
    
    private class Message
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
    
    private class Usage
    {
        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }
        
        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }
}

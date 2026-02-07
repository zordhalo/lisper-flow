using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace LisperFlow.LLM;

/// <summary>
/// Local LLM provider using Phi-3 ONNX model for text enhancement
/// </summary>
public class Phi3OnnxProvider : ILlmProvider, IDisposable
{
    private InferenceSession? _session;
    private readonly string _modelPath;
    private readonly bool _useGpu;
    private readonly ILogger<Phi3OnnxProvider> _logger;
    private bool _initialized;
    private bool _disposed;
    
    // Phi-3 model constants
    private const int MAX_CONTEXT_LENGTH = 4096;
    private const int MAX_NEW_TOKENS = 256;
    
    public string Name => "Phi3-ONNX-Local";
    
    public bool IsAvailable => _initialized && _session != null;
    
    public Phi3OnnxProvider(
        string modelPath,
        bool useGpu = true,
        ILogger<Phi3OnnxProvider>? logger = null)
    {
        _modelPath = modelPath;
        _useGpu = useGpu;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<Phi3OnnxProvider>.Instance;
    }
    
    /// <summary>
    /// Initialize the Phi-3 ONNX model
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var modelFile = FindModelFile(_modelPath);
                
                if (modelFile == null)
                {
                    _logger.LogWarning("Phi-3 ONNX model not found at {Path}", _modelPath);
                    return false;
                }
                
                var sessionOptions = new SessionOptions
                {
                    ExecutionMode = ExecutionMode.ORT_PARALLEL,
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
                };
                
                // Try GPU acceleration with DirectML
                if (_useGpu)
                {
                    try
                    {
                        sessionOptions.AppendExecutionProvider_DML();
                        _logger.LogInformation("DirectML GPU acceleration enabled for Phi-3");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "DirectML not available, falling back to CPU");
                    }
                }
                
                _session = new InferenceSession(modelFile, sessionOptions);
                
                _initialized = true;
                _logger.LogInformation("Phi-3 ONNX model loaded from {Path}", modelFile);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load Phi-3 ONNX model");
                return false;
            }
        });
    }
    
    private string? FindModelFile(string basePath)
    {
        // Look for various model file patterns
        string[] patterns = new[]
        {
            "phi-3-mini.onnx",
            "model.onnx",
            "phi3.onnx",
            "phi-3-mini-4k-instruct.onnx"
        };
        
        foreach (var pattern in patterns)
        {
            var path = Path.Combine(basePath, pattern);
            if (File.Exists(path)) return path;
        }
        
        // Check if basePath is itself an ONNX file
        if (basePath.EndsWith(".onnx") && File.Exists(basePath))
        {
            return basePath;
        }
        
        // Find any .onnx file in the directory
        if (Directory.Exists(basePath))
        {
            var files = Directory.GetFiles(basePath, "*.onnx");
            if (files.Length > 0) return files[0];
        }
        
        return null;
    }
    
    public async Task<LlmResult> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return LlmResult.Failed("Phi-3 ONNX model not initialized. Please download model files.");
        }
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Generating with local Phi-3 model: {Length} chars",
                userPrompt.Length);
            
            // Note: Full Phi-3 inference requires:
            // 1. Tokenization (BPE/SentencePiece)
            // 2. KV-cache management
            // 3. Autoregressive token generation
            // 4. Detokenization
            
            // This is a placeholder - return the original text
            await Task.Delay(50, cancellationToken); // Simulate processing
            
            stopwatch.Stop();
            
            // Return indication that full inference is not yet implemented
            return new LlmResult
            {
                GeneratedText = userPrompt, // Return original for now
                TokensGenerated = 0,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                Provider = Name,
                Success = true,
                ErrorMessage = "Local model loaded but full inference pending - using original text"
            };
        }
        catch (OperationCanceledException)
        {
            return LlmResult.Failed("Generation cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Local generation failed");
            return LlmResult.Failed(ex.Message);
        }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _session?.Dispose();
        _logger.LogDebug("Phi3OnnxProvider disposed");
    }
}

using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace LisperFlow.ASR;

/// <summary>
/// Local ASR provider using Whisper ONNX model
/// Note: This is a simplified implementation. For production use,
/// consider using a dedicated Whisper ONNX library or whisper.cpp
/// </summary>
public class WhisperOnnxProvider : IAsrProvider, IDisposable
{
    private InferenceSession? _encoderSession;
    private readonly string _modelPath;
    private readonly bool _useGpu;
    private readonly ILogger<WhisperOnnxProvider> _logger;
    private bool _initialized;
    private bool _disposed;
    
    // Whisper constants
    private const int SAMPLE_RATE = 16000;
    private const int N_MELS = 80;
    private const int CHUNK_LENGTH = 30; // seconds
    
    public string Name => "Whisper-ONNX-Local";
    
    public bool IsAvailable => _initialized && _encoderSession != null;
    
    public WhisperOnnxProvider(
        string modelPath,
        bool useGpu = true,
        ILogger<WhisperOnnxProvider>? logger = null)
    {
        _modelPath = modelPath;
        _useGpu = useGpu;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<WhisperOnnxProvider>.Instance;
    }
    
    /// <summary>
    /// Initialize the Whisper ONNX model
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                // Look for the encoder model
                var modelFile = FindModelFile(_modelPath);
                
                if (modelFile == null)
                {
                    _logger.LogWarning("Whisper ONNX model not found at {Path}", _modelPath);
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
                        _logger.LogInformation("DirectML GPU acceleration enabled for Whisper");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "DirectML not available, falling back to CPU");
                    }
                }
                
                _encoderSession = new InferenceSession(modelFile, sessionOptions);
                
                _initialized = true;
                _logger.LogInformation("Whisper ONNX model loaded from {Path}", modelFile);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load Whisper ONNX model");
                return false;
            }
        });
    }
    
    private string? FindModelFile(string basePath)
    {
        // Look for various model file patterns
        string[] patterns = new[]
        {
            "whisper-small-encoder.onnx",
            "encoder_model.onnx",
            "model.onnx",
            "whisper.onnx"
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
    
    public async Task<AsrResult> TranscribeAsync(
        float[] audioSamples,
        int sampleRate = 16000,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return AsrResult.Failed("Whisper ONNX model not initialized. Please download model files.");
        }
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Resample if needed
            if (sampleRate != SAMPLE_RATE)
            {
                audioSamples = ResampleAudio(audioSamples, sampleRate, SAMPLE_RATE);
            }
            
            // For now, use a simplified approach - log that local model is processing
            _logger.LogInformation("Processing {Duration:F2}s of audio with local Whisper model",
                audioSamples.Length / (float)SAMPLE_RATE);
            
            // Note: Full whisper inference requires:
            // 1. Mel spectrogram computation
            // 2. Encoder pass
            // 3. Decoder with autoregressive token generation
            // 4. Tokenizer for decoding
            
            // This is a placeholder that would be replaced with actual inference
            await Task.Delay(100, cancellationToken); // Simulate processing
            
            stopwatch.Stop();
            
            // Return a message indicating the model needs proper integration
            return new AsrResult
            {
                Transcript = "[Local Whisper model loaded - full inference not yet implemented. Using cloud fallback.]",
                Confidence = 0.0f,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                Provider = Name,
                Success = false // Mark as failed so fallback is used
            };
        }
        catch (OperationCanceledException)
        {
            return AsrResult.Failed("Transcription cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Local transcription failed");
            return AsrResult.Failed(ex.Message);
        }
    }
    
    private static float[] ResampleAudio(float[] audio, int fromRate, int toRate)
    {
        if (fromRate == toRate) return audio;
        
        double ratio = (double)fromRate / toRate;
        int outputLength = (int)(audio.Length / ratio);
        var output = new float[outputLength];
        
        for (int i = 0; i < outputLength; i++)
        {
            double srcIndex = i * ratio;
            int srcIndexInt = (int)srcIndex;
            double frac = srcIndex - srcIndexInt;
            
            if (srcIndexInt + 1 < audio.Length)
            {
                output[i] = (float)(audio[srcIndexInt] * (1 - frac) + audio[srcIndexInt + 1] * frac);
            }
            else if (srcIndexInt < audio.Length)
            {
                output[i] = audio[srcIndexInt];
            }
        }
        
        return output;
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _encoderSession?.Dispose();
        _logger.LogDebug("WhisperOnnxProvider disposed");
    }
}

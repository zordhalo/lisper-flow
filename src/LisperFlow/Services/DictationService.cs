using System.Diagnostics;
using LisperFlow.ASR;
using LisperFlow.AudioCapture;
using LisperFlow.Configuration;
using LisperFlow.Context;
using LisperFlow.Hotkeys;
using LisperFlow.LLM;
using LisperFlow.Streaming;
using LisperFlow.Streaming.Models;
using LisperFlow.TextInjection;
using Microsoft.Extensions.Logging;

namespace LisperFlow.Services;

/// <summary>
/// Main orchestrator for the voice dictation workflow.
/// Coordinates audio capture, ASR, LLM enhancement, and text injection.
/// </summary>
public class DictationService : IDisposable
{
    private readonly AudioCaptureService _audioService;
    private readonly HotkeyRegistrar _hotkeyRegistrar;
    private readonly IAsrProvider _asrProvider;
    private readonly ILlmProvider? _llmProvider;
    private readonly InjectionStrategySelector _injectionStrategy;
    private readonly PromptTemplateEngine _promptEngine;
    private readonly FocusedElementDetector _focusDetector;
    private readonly AppSettings _settings;
    private readonly ILogger<DictationService> _logger;
    private readonly IStreamingAsrProvider _streamingAsrProvider;
    private readonly StreamingCoordinator _streamingCoordinator;
    private readonly TranscriptSynchronizer _transcriptSynchronizer;
    private readonly TypingCommandQueue _typingQueue;
    private readonly RealTimeTextInjector _realTimeInjector;
    
    private DictationState _state = DictationState.Idle;
    private CancellationTokenSource? _currentCts;
    private bool _disposed;
    private bool _isStreaming;
    private IntPtr _streamingTargetWindow;
    
    /// <summary>
    /// Current dictation state
    /// </summary>
    public DictationState State => _state;
    
    /// <summary>
    /// Event raised when state changes
    /// </summary>
    public event EventHandler<DictationStateChangedEventArgs>? StateChanged;
    
    /// <summary>
    /// Event raised when transcription is complete (before injection)
    /// </summary>
    public event EventHandler<TranscriptionCompleteEventArgs>? TranscriptionComplete;
    
    /// <summary>
    /// Event raised when text is injected
    /// </summary>
    public event EventHandler<TextInjectedEventArgs>? TextInjected;
    
    /// <summary>
    /// Event raised when an error occurs
    /// </summary>
    public event EventHandler<DictationErrorEventArgs>? ErrorOccurred;
    
    public DictationService(
        AudioCaptureService audioService,
        HotkeyRegistrar hotkeyRegistrar,
        IAsrProvider asrProvider,
        ILlmProvider? llmProvider,
        InjectionStrategySelector injectionStrategy,
        PromptTemplateEngine promptEngine,
        FocusedElementDetector focusDetector,
        AppSettings settings,
        IStreamingAsrProvider streamingAsrProvider,
        StreamingCoordinator streamingCoordinator,
        TranscriptSynchronizer transcriptSynchronizer,
        TypingCommandQueue typingQueue,
        RealTimeTextInjector realTimeInjector,
        ILogger<DictationService> logger)
    {
        _audioService = audioService;
        _hotkeyRegistrar = hotkeyRegistrar;
        _asrProvider = asrProvider;
        _llmProvider = llmProvider;
        _injectionStrategy = injectionStrategy;
        _promptEngine = promptEngine;
        _focusDetector = focusDetector;
        _settings = settings;
        _logger = logger;
        _streamingAsrProvider = streamingAsrProvider;
        _streamingCoordinator = streamingCoordinator;
        _transcriptSynchronizer = transcriptSynchronizer;
        _typingQueue = typingQueue;
        _realTimeInjector = realTimeInjector;
        
        // Wire up hotkey
        _hotkeyRegistrar.HotkeyPressed += OnHotkeyPressed;
        
        // Wire up speech detection
        _audioService.SpeechStarted += OnSpeechStarted;
        _audioService.SpeechEnded += OnSpeechEnded;
        
        _streamingCoordinator.PartialTranscriptReceived += OnPartialTranscriptReceived;
        _streamingCoordinator.FinalTranscriptReceived += OnFinalTranscriptReceived;
    }
    
    /// <summary>
    /// Initialize and start the dictation service
    /// </summary>
    public async Task<bool> StartAsync()
    {
        try
        {
            _logger.LogInformation("Starting dictation service...");
            
            // Initialize audio capture
            bool audioOk = await _audioService.InitializeAsync(
                _settings.Audio.DeviceId,
                null // VAD model path - will use energy-based fallback for now
            );
            
            if (!audioOk)
            {
                _logger.LogError("Failed to initialize audio capture");
                return false;
            }
            
            // Register dictation hotkey
            var hotkeyConfig = HotkeyConfig.FromSettings(_settings.Hotkey, "Dictation");
            
            try
            {
                _hotkeyRegistrar.RegisterHotkey(hotkeyConfig);
                _logger.LogInformation(
                    "Dictation hotkey registered: {Hotkey}",
                    hotkeyConfig.GetDisplayString());
            }
            catch (HotkeyAlreadyRegisteredException ex)
            {
                _logger.LogWarning(
                    "Hotkey {Hotkey} already registered: {Error}",
                    ex.Config?.GetDisplayString(),
                    ex.Message);
                
                // Try fallback alternatives
                var (success, alternatives) = _hotkeyRegistrar.TryRegisterWithFallback(hotkeyConfig);
                if (!success && alternatives.Count > 0)
                {
                    foreach (var alt in alternatives)
                    {
                        try
                        {
                            _hotkeyRegistrar.RegisterHotkey(alt);
                            _logger.LogInformation(
                                "Using alternative hotkey: {Hotkey}",
                                alt.GetDisplayString());
                            break;
                        }
                        catch { }
                    }
                }
            }
            
            SetState(DictationState.Ready);
            _logger.LogInformation("Dictation service started successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start dictation service");
            SetState(DictationState.Error);
            return false;
        }
    }
    
    /// <summary>
    /// Stop the dictation service
    /// </summary>
    public async Task StopAsync()
    {
        _logger.LogInformation("Stopping dictation service...");
        
        _currentCts?.Cancel();
        
        if (_state == DictationState.Recording)
        {
            await _audioService.StopListeningAsync();
        }
        else if (_state == DictationState.Streaming)
        {
            await StopStreamingAsync();
        }
        
        _hotkeyRegistrar.UnregisterAll();
        SetState(DictationState.Idle);
        
        _logger.LogInformation("Dictation service stopped");
    }
    
    /// <summary>
    /// Toggle recording state (for push-to-talk)
    /// </summary>
    public async Task ToggleRecordingAsync()
    {
        if (_state == DictationState.Recording)
        {
            await StopRecordingAsync();
        }
        else if (_state == DictationState.Ready)
        {
            await StartRecordingAsync();
        }
    }
    
    public async Task ToggleStreamingAsync()
    {
        if (_state == DictationState.Streaming)
        {
            await StopStreamingAsync();
        }
        else if (_state == DictationState.Ready)
        {
            await StartStreamingAsync();
        }
    }
    
    /// <summary>
    /// Start recording audio
    /// </summary>
    public async Task StartRecordingAsync()
    {
        if (_state != DictationState.Ready) return;
        
        SetState(DictationState.Recording);
        _currentCts = new CancellationTokenSource();
        
        try
        {
            await _audioService.StartListeningAsync();
            _logger.LogInformation("Recording started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start recording");
            HandleError("Failed to start recording", ex);
            SetState(DictationState.Ready);
        }
    }
    
    /// <summary>
    /// Stop recording and process the audio
    /// </summary>
    public async Task StopRecordingAsync()
    {
        if (_state != DictationState.Recording) return;
        
        var cts = _currentCts;
        
        try
        {
            // Stop recording and get final audio
            var audioSamples = await _audioService.StopListeningAsync();
            
            if (audioSamples == null || audioSamples.Length == 0)
            {
                _logger.LogWarning("No audio captured");
                SetState(DictationState.Ready);
                return;
            }
            
            // Process the audio
            await ProcessAudioAsync(audioSamples, cts?.Token ?? CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing recording");
            HandleError("Error processing recording", ex);
            SetState(DictationState.Ready);
        }
    }
    
    private async Task ProcessAudioAsync(float[] audioSamples, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            SetState(DictationState.Transcribing);
            
            float durationSeconds = audioSamples.Length / (float)_settings.Audio.SampleRate;
            _logger.LogDebug("Processing {Duration:F2}s of audio", durationSeconds);
            
            // Step 1: ASR transcription
            var asrResult = await _asrProvider.TranscribeAsync(
                audioSamples,
                _settings.Audio.SampleRate,
                cancellationToken);
            
            if (!asrResult.Success || string.IsNullOrWhiteSpace(asrResult.Transcript))
            {
                _logger.LogWarning("ASR failed or returned empty result: {Error}", asrResult.ErrorMessage);
                SetState(DictationState.Ready);
                return;
            }
            
            string transcript = asrResult.Transcript;
            
            // Raise event with raw transcript
            TranscriptionComplete?.Invoke(this, new TranscriptionCompleteEventArgs
            {
                RawTranscript = transcript,
                AudioDuration = TimeSpan.FromSeconds(durationSeconds),
                AsrLatencyMs = asrResult.LatencyMs
            });
            
            // Step 2: LLM enhancement (if enabled and available)
            if (_settings.Llm.EnableEnhancement && _llmProvider?.IsAvailable == true)
            {
                SetState(DictationState.Enhancing);
                
                // Get context from focused element
                var focusedElement = _focusDetector.GetFocusedElement();
                
                var context = new EnhancementContext
                {
                    RawTranscript = transcript,
                    ApplicationName = focusedElement?.ProcessName ?? "",
                    WindowTitle = focusedElement?.WindowTitle ?? "",
                    TonePreference = focusedElement != null 
                        ? ApplicationContext.InferTone(focusedElement.ProcessName)
                        : ToneType.Default
                };
                
                var systemPrompt = _promptEngine.BuildSystemPrompt(context);
                var userPrompt = _promptEngine.BuildUserPrompt(transcript);
                
                var llmResult = await _llmProvider.GenerateAsync(
                    systemPrompt,
                    userPrompt,
                    cancellationToken);
                
                if (llmResult.Success && !string.IsNullOrWhiteSpace(llmResult.GeneratedText))
                {
                    transcript = llmResult.GeneratedText;
                    _logger.LogDebug("Enhanced transcript: {Preview}", 
                        transcript.Length > 50 ? transcript[..50] + "..." : transcript);
                }
            }
            
            // Step 3: Text injection
            SetState(DictationState.Injecting);
            
            var injectionResult = await _injectionStrategy.InjectTextAsync(transcript, cancellationToken);
            
            if (injectionResult.Success)
            {
                stopwatch.Stop();
                
                _logger.LogInformation(
                    "Dictation complete: {Length} chars injected via {Strategy} in {Total}ms total",
                    transcript.Length,
                    injectionResult.StrategyUsed,
                    stopwatch.ElapsedMilliseconds);
                
                TextInjected?.Invoke(this, new TextInjectedEventArgs
                {
                    Text = transcript,
                    Strategy = injectionResult.StrategyUsed,
                    TotalLatencyMs = stopwatch.ElapsedMilliseconds
                });
            }
            else
            {
                _logger.LogWarning("Text injection failed: {Error}", injectionResult.ErrorMessage);
                HandleError("Text injection failed", new Exception(injectionResult.ErrorMessage));
            }
            
            SetState(DictationState.Ready);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Dictation cancelled");
            SetState(DictationState.Ready);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in audio processing pipeline");
            HandleError("Processing error", ex);
            SetState(DictationState.Ready);
        }
    }
    
    private void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        _logger.LogDebug("Dictation hotkey pressed: {Name}", e.Name);
        
        // Toggle recording on UI thread
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(async () =>
        {
            if (_settings.Streaming.Enabled)
            {
                await ToggleStreamingAsync();
            }
            else
            {
                await ToggleRecordingAsync();
            }
        });
    }
    
    private void OnSpeechStarted(object? sender, EventArgs e)
    {
        _logger.LogDebug("Speech detected");
    }
    
    private void OnSpeechEnded(object? sender, EventArgs e)
    {
        _logger.LogDebug("Speech ended");
    }
    
    private void SetState(DictationState state)
    {
        var previous = _state;
        _state = state;
        
        StateChanged?.Invoke(this, new DictationStateChangedEventArgs
        {
            PreviousState = previous,
            CurrentState = state
        });
    }
    
    private void HandleError(string message, Exception? ex)
    {
        ErrorOccurred?.Invoke(this, new DictationErrorEventArgs
        {
            Message = message,
            Exception = ex
        });
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _currentCts?.Cancel();
        _currentCts?.Dispose();
        
        _hotkeyRegistrar.HotkeyPressed -= OnHotkeyPressed;
        _audioService.SpeechStarted -= OnSpeechStarted;
        _audioService.SpeechEnded -= OnSpeechEnded;
        _streamingCoordinator.PartialTranscriptReceived -= OnPartialTranscriptReceived;
        _streamingCoordinator.FinalTranscriptReceived -= OnFinalTranscriptReceived;
        
        _audioService.Dispose();
        _hotkeyRegistrar.Dispose();
        _streamingCoordinator.Dispose();
        _realTimeInjector.Dispose();
        
        if (_asrProvider is IDisposable disposableAsr)
            disposableAsr.Dispose();
        
        if (_llmProvider is IDisposable disposableLlm)
            disposableLlm.Dispose();
        
        if (_streamingAsrProvider is IDisposable disposableStreaming)
            disposableStreaming.Dispose();
        
        _logger.LogDebug("DictationService disposed");
    }

    private async Task StartStreamingAsync()
    {
        try
        {
            SetState(DictationState.Streaming);
            _currentCts = new CancellationTokenSource();

            // Ensure a streaming provider is configured before starting
            if (string.IsNullOrWhiteSpace(_settings.Streaming.Provider))
            {
                throw new InvalidOperationException("Streaming provider is not configured.");
            }
            
            var focusedElement = _focusDetector.GetFocusedElement();
            _streamingTargetWindow = focusedElement?.WindowHandle ?? Hotkeys.Win32Interop.GetForegroundWindow();
            _logger.LogInformation(
                "Streaming target: {Process} - {Title} ({Handle})",
                focusedElement?.ProcessName ?? "Unknown",
                focusedElement?.WindowTitle ?? "",
                _streamingTargetWindow);
            _transcriptSynchronizer.Reset();
            _typingQueue.Clear();
            
            // Start ASR streaming before typing to avoid dangling injector
            await _streamingCoordinator.StartStreamingAsync(_currentCts.Token);
            _realTimeInjector.Start(_streamingTargetWindow);
            _isStreaming = true;
            
            _logger.LogInformation("Streaming dictation started");
        }
        catch (Exception ex)
        {
            try
            {
                await _realTimeInjector.StopAsync();
            }
            catch { }
            
            _logger.LogError(ex, "Failed to start streaming");
            HandleError("Failed to start streaming", ex);
            SetState(DictationState.Ready);
        }
    }
    
    private async Task StopStreamingAsync()
    {
        if (!_isStreaming) return;
        
        try
        {
            _currentCts?.Cancel();
            await _streamingCoordinator.StopStreamingAsync();
            await Task.Delay(200);
            await _realTimeInjector.StopAsync();
            
            _transcriptSynchronizer.Reset();
            _typingQueue.Clear();
            
            _isStreaming = false;
            SetState(DictationState.Ready);
            
            _logger.LogInformation("Streaming dictation stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop streaming");
            HandleError("Failed to stop streaming", ex);
            SetState(DictationState.Ready);
        }
    }
    
    private void OnPartialTranscriptReceived(object? sender, PartialTranscriptEventArgs e)
    {
        if (!_settings.Streaming.ShowPartialResults) return;
        
        _logger.LogDebug("Streaming partial transcript: {Text}", e.Text);
        var update = _transcriptSynchronizer.ProcessPartialTranscript(e.Text);
        foreach (var word in update.WordsToType)
        {
            _typingQueue.EnqueueWord(word);
        }
        // Ignore corrections from partials - append only
    }
    
    private void OnFinalTranscriptReceived(object? sender, FinalTranscriptEventArgs e)
    {
        _logger.LogInformation("Streaming final transcript: {Text}", e.Text);
        var update = _transcriptSynchronizer.ProcessPartialTranscript(e.Text);
        foreach (var word in update.WordsToType)
        {
            _typingQueue.EnqueueWord(word);
        }
        // Don't apply corrections - just commit and reset for next utterance
        _transcriptSynchronizer.Reset();
        
        if (_settings.Streaming.ApplyLlmEnhancement.Equals("AfterFinalization", StringComparison.OrdinalIgnoreCase) &&
            _llmProvider?.IsAvailable == true)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var prompt = "Fix grammar, punctuation, and filler words. Keep meaning.\n\nTranscript:\n" + e.Text;
                    var result = await _llmProvider.GenerateAsync("You are a text enhancement assistant.", prompt);
                    if (result.Success && !string.IsNullOrWhiteSpace(result.GeneratedText))
                    {
                        _logger.LogDebug("Streaming LLM enhancement ready (len {Len})", result.GeneratedText.Length);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Streaming LLM enhancement failed");
                }
            });
        }
    }
}

/// <summary>
/// Dictation service states
/// </summary>
public enum DictationState
{
    Idle,
    Ready,
    Recording,
    Streaming,
    Transcribing,
    Enhancing,
    Injecting,
    Error
}

/// <summary>
/// Event args for state changes
/// </summary>
public class DictationStateChangedEventArgs : EventArgs
{
    public DictationState PreviousState { get; set; }
    public DictationState CurrentState { get; set; }
}

/// <summary>
/// Event args for transcription complete
/// </summary>
public class TranscriptionCompleteEventArgs : EventArgs
{
    public string RawTranscript { get; set; } = "";
    public TimeSpan AudioDuration { get; set; }
    public long AsrLatencyMs { get; set; }
}

/// <summary>
/// Event args for text injected
/// </summary>
public class TextInjectedEventArgs : EventArgs
{
    public string Text { get; set; } = "";
    public string Strategy { get; set; } = "";
    public long TotalLatencyMs { get; set; }
}

/// <summary>
/// Event args for errors
/// </summary>
public class DictationErrorEventArgs : EventArgs
{
    public string Message { get; set; } = "";
    public Exception? Exception { get; set; }
}

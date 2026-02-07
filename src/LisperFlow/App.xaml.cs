using System.IO;
using System.Text.Json;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using LisperFlow.ASR;
using LisperFlow.AudioCapture;
using LisperFlow.Configuration;
using LisperFlow.Hotkeys;
using LisperFlow.LLM;
using LisperFlow.Services;
using LisperFlow.TextInjection;
using LisperFlow.TextInjection.Strategies;
using LisperFlow.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace LisperFlow;

/// <summary>
/// Main application entry point with DI container setup
/// </summary>
public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    private TaskbarIcon? _taskbarIcon;
    private DictationService? _dictationService;
    private AppSettings? _appSettings;
    
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "lisperflow-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();
        
        try
        {
            // Load settings
            _appSettings = await LoadSettingsAsync();
            
            // Build service container
            var services = new ServiceCollection();
            ConfigureServices(services, _appSettings);
            _serviceProvider = services.BuildServiceProvider();
            
            // Get the dictation service
            _dictationService = _serviceProvider.GetRequiredService<DictationService>();
            
            // Create system tray icon
            CreateSystemTrayIcon();
            
            // Start the dictation service
            bool started = await _dictationService.StartAsync();
            
            if (started)
            {
                Log.Information("LisperFlow started successfully");
            }
            else
            {
                Log.Error("Failed to start LisperFlow");
                MessageBox.Show(
                    "Failed to initialize LisperFlow. Check logs for details.",
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application startup failed");
            MessageBox.Show(
                $"Application startup failed: {ex.Message}",
                "Fatal Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }
    
    protected override async void OnExit(ExitEventArgs e)
    {
        Log.Information("LisperFlow shutting down...");
        
        if (_dictationService != null)
        {
            await _dictationService.StopAsync();
            _dictationService.Dispose();
        }
        
        _taskbarIcon?.Dispose();
        
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
        
        Log.CloseAndFlush();
        
        base.OnExit(e);
    }
    
    private void ConfigureServices(IServiceCollection services, AppSettings settings)
    {
        // Register settings
        services.AddSingleton(settings);
        
        // Add logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });
        
        // Audio configuration
        var audioConfig = new AudioConfiguration
        {
            DeviceId = settings.Audio.DeviceId,
            SampleRate = settings.Audio.SampleRate,
            VadThreshold = settings.Audio.VadThreshold,
            BufferSeconds = settings.Audio.BufferSeconds,
            PreRollMs = settings.Audio.PreRollMs
        };
        services.AddSingleton(audioConfig);
        
        // Audio services - factory method to inject all required loggers
        services.AddSingleton<AudioCaptureService>(sp => new AudioCaptureService(
            sp.GetRequiredService<AudioConfiguration>(),
            sp.GetRequiredService<ILogger<AudioCaptureService>>(),
            sp.GetRequiredService<ILogger<WasapiCaptureManager>>(),
            sp.GetRequiredService<ILogger<VoiceActivityDetector>>()
        ));
        
        // Hotkey services - factory method
        services.AddSingleton<HotkeyRegistrar>(sp => new HotkeyRegistrar(
            sp.GetRequiredService<ILogger<HotkeyRegistrar>>()
        ));
        
        // ASR providers
        services.AddSingleton<IAsrProvider>(sp =>
            new OpenAIWhisperProvider(
                settings.Asr.OpenAiApiKey ?? "",
                sp.GetRequiredService<ILogger<OpenAIWhisperProvider>>()));
        
        // LLM providers
        if (settings.Llm.EnableEnhancement && !string.IsNullOrEmpty(settings.Llm.OpenAiApiKey))
        {
            services.AddSingleton<ILlmProvider>(sp =>
                new OpenAIProvider(
                    settings.Llm.OpenAiApiKey!,
                    settings.Llm.CloudModel,
                    sp.GetRequiredService<ILogger<OpenAIProvider>>()));
        }
        // else block removed to avoid CS8634

        
        // Text injection services
        services.AddSingleton<FocusedElementDetector>(sp => new FocusedElementDetector(
            sp.GetRequiredService<ILogger<FocusedElementDetector>>()
        ));
        services.AddSingleton<InjectionStrategySelector>(sp => new InjectionStrategySelector(
            sp.GetRequiredService<FocusedElementDetector>(),
            sp.GetRequiredService<ILogger<InjectionStrategySelector>>(),
            sp.GetRequiredService<ILogger<ClipboardPasteInjector>>(),
            sp.GetRequiredService<ILogger<SendInputInjector>>()
        ));
        services.AddSingleton<PromptTemplateEngine>();
        
        // Main orchestrator - explicit factory to handle optional ILlmProvider
        services.AddSingleton<DictationService>(sp => new DictationService(
            sp.GetRequiredService<AudioCaptureService>(),
            sp.GetRequiredService<HotkeyRegistrar>(),
            sp.GetRequiredService<IAsrProvider>(),
            sp.GetService<ILlmProvider>(), // Optional dependency
            sp.GetRequiredService<InjectionStrategySelector>(),
            sp.GetRequiredService<PromptTemplateEngine>(),
            sp.GetRequiredService<FocusedElementDetector>(),
            sp.GetRequiredService<AppSettings>(),
            sp.GetRequiredService<ILogger<DictationService>>()
        ));
        
        // UI
        services.AddSingleton<SystemTrayViewModel>();
    }
    
    private void CreateSystemTrayIcon()
    {
        _taskbarIcon = new TaskbarIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            ToolTipText = "LisperFlow - Ready"
        };
        
        // Create context menu
        var contextMenu = new System.Windows.Controls.ContextMenu();
        
        var recordItem = new System.Windows.Controls.MenuItem { Header = "Toggle Recording (Ctrl+Win+Space)" };
        recordItem.Click += async (s, e) =>
        {
            if (_dictationService != null)
                await _dictationService.ToggleRecordingAsync();
        };
        
        var settingsItem = new System.Windows.Controls.MenuItem { Header = "Settings..." };
        settingsItem.Click += (s, e) =>
        {
            OpenSettingsWindow();
        };
        
        var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exitItem.Click += (s, e) => Shutdown();
        
        contextMenu.Items.Add(recordItem);
        contextMenu.Items.Add(new System.Windows.Controls.Separator());
        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(new System.Windows.Controls.Separator());
        contextMenu.Items.Add(exitItem);
        
        _taskbarIcon.ContextMenu = contextMenu;
        
        // Wire up state changes for tooltip
        if (_dictationService != null)
        {
            _dictationService.StateChanged += (s, e) =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    _taskbarIcon.ToolTipText = $"LisperFlow - {e.CurrentState}";
                });
            };
        }
    }
    
    private async Task<AppSettings> LoadSettingsAsync()
    {
        var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        
        if (File.Exists(settingsPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (settings != null)
                {
                    Log.Information("Loaded settings from {Path}", settingsPath);
                    return settings;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load settings, using defaults");
            }
        }
        
        Log.Information("Using default settings");
        return new AppSettings();
    }
    
    private void OpenSettingsWindow()
    {
        if (_appSettings == null) return;
        
        var audioService = _serviceProvider?.GetService<AudioCaptureService>();
        var settingsWindow = new SettingsWindow(_appSettings, audioService);
        
        if (settingsWindow.ShowDialog() == true)
        {
            // Settings saved - notify user that restart may be needed
            MessageBox.Show(
                "Settings saved successfully.\n\nSome changes may require restarting LisperFlow to take effect.",
                "Settings Saved",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}

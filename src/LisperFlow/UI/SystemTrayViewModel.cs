using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LisperFlow.Services;
using System.Windows;
using System.Windows.Media;

namespace LisperFlow.UI;

/// <summary>
/// View model for the system tray application
/// </summary>
public partial class SystemTrayViewModel : ObservableObject
{
    private readonly DictationService _dictationService;
    
    [ObservableProperty]
    private string _statusText = "Ready";
    
    [ObservableProperty]
    private string _hotkeyDisplay = "Ctrl+Win+Space";
    
    [ObservableProperty]
    private Brush _statusColor = Brushes.Green;
    
    [ObservableProperty]
    private bool _isRecording;
    
    [ObservableProperty]
    private string _lastTranscript = "";
    
    [ObservableProperty]
    private string _tooltipText = "LisperFlow - Ready";
    
    public SystemTrayViewModel(DictationService dictationService)
    {
        _dictationService = dictationService;
        
        // Subscribe to events
        _dictationService.StateChanged += OnStateChanged;
        _dictationService.TranscriptionComplete += OnTranscriptionComplete;
        _dictationService.TextInjected += OnTextInjected;
        _dictationService.ErrorOccurred += OnErrorOccurred;
    }
    
    [RelayCommand]
    private async Task ToggleRecording()
    {
        await _dictationService.ToggleRecordingAsync();
    }
    
    [RelayCommand]
    private void ShowSettings()
    {
        // TODO: Show settings window
        MessageBox.Show(
            "Settings UI coming soon!\n\nCurrent hotkey: " + HotkeyDisplay,
            "LisperFlow Settings",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
    
    [RelayCommand]
    private void Exit()
    {
        Application.Current.Shutdown();
    }
    
    private void OnStateChanged(object? sender, DictationStateChangedEventArgs e)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            (StatusText, StatusColor) = e.CurrentState switch
            {
                DictationState.Idle => ("Idle", Brushes.Gray),
                DictationState.Ready => ("Ready", Brushes.Green),
                DictationState.Recording => ("Recording...", Brushes.Red),
                DictationState.Transcribing => ("Transcribing...", Brushes.Orange),
                DictationState.Enhancing => ("Enhancing...", Brushes.Purple),
                DictationState.Injecting => ("Typing...", Brushes.Blue),
                DictationState.Error => ("Error", Brushes.DarkRed),
                _ => ("Unknown", Brushes.Gray)
            };
            
            IsRecording = e.CurrentState == DictationState.Recording;
            TooltipText = $"LisperFlow - {StatusText}";
        });
    }
    
    private void OnTranscriptionComplete(object? sender, TranscriptionCompleteEventArgs e)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            LastTranscript = e.RawTranscript;
        });
    }
    
    private void OnTextInjected(object? sender, TextInjectedEventArgs e)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            // Could show a toast notification here
        });
    }
    
    private void OnErrorOccurred(object? sender, DictationErrorEventArgs e)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            StatusText = "Error";
            StatusColor = Brushes.DarkRed;
            
            // Show error briefly (in real app, might use toast)
            MessageBox.Show(
                e.Message,
                "LisperFlow Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        });
    }
    
    public void Cleanup()
    {
        _dictationService.StateChanged -= OnStateChanged;
        _dictationService.TranscriptionComplete -= OnTranscriptionComplete;
        _dictationService.TextInjected -= OnTextInjected;
        _dictationService.ErrorOccurred -= OnErrorOccurred;
    }
}

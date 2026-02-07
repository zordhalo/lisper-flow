using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using LisperFlow.AudioCapture;
using LisperFlow.Configuration;
using Microsoft.Win32;

namespace LisperFlow.UI;

/// <summary>
/// Settings window for LisperFlow configuration
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly AudioCaptureService? _audioService;
    private List<AudioDevice> _audioDevices = new();
    private Key _capturedKey = Key.Space;
    private ModifierKeys _capturedModifiers = ModifierKeys.Control;
    private bool _isCapturingHotkey;
    
    public SettingsWindow(AppSettings settings, AudioCaptureService? audioService = null)
    {
        InitializeComponent();
        _settings = settings;
        _audioService = audioService;
        
        LoadSettings();
        LoadAudioDevices();
    }
    
    private void LoadSettings()
    {
        // Hotkey
        UpdateHotkeyDisplay();
        
        // ASR settings
        if (_settings.Asr.Provider == "Local")
        {
            AsrProviderCombo.SelectedIndex = 1;
            AsrApiKeyPanel.Visibility = Visibility.Collapsed;
            AsrLocalPanel.Visibility = Visibility.Visible;
        }
        else
        {
            AsrProviderCombo.SelectedIndex = 0;
            AsrApiKeyPanel.Visibility = Visibility.Visible;
            AsrLocalPanel.Visibility = Visibility.Collapsed;
        }
        AsrApiKeyTextBox.Text = _settings.Asr.OpenAiApiKey ?? "";
        AsrModelPathTextBox.Text = _settings.Asr.LocalModelPath ?? "";
        
        // LLM settings
        EnableEnhancementCheckBox.IsChecked = _settings.Llm.EnableEnhancement;
        if (_settings.Llm.Provider == "Local")
        {
            LlmProviderCombo.SelectedIndex = 1;
            LlmApiKeyPanel.Visibility = Visibility.Collapsed;
            LlmLocalPanel.Visibility = Visibility.Visible;
        }
        else
        {
            LlmProviderCombo.SelectedIndex = 0;
            LlmApiKeyPanel.Visibility = Visibility.Visible;
            LlmLocalPanel.Visibility = Visibility.Collapsed;
        }
        LlmApiKeyTextBox.Text = _settings.Llm.OpenAiApiKey ?? "";
        LlmModelPathTextBox.Text = _settings.Llm.LocalModelPath ?? "";
        
        // Audio settings
        UseGpuCheckBox.IsChecked = _settings.Asr.UseGpu;
    }
    
    private void LoadAudioDevices()
    {
        MicrophoneCombo.Items.Clear();
        _audioDevices.Clear();
        
        // Add default device option
        MicrophoneCombo.Items.Add("Default");
        
        // Get available devices from audio service
        if (_audioService != null)
        {
            _audioDevices = _audioService.GetDevices().ToList();
            foreach (var device in _audioDevices)
            {
                MicrophoneCombo.Items.Add(device.FriendlyName);
            }
        }
        
        // Select current device
        if (string.IsNullOrEmpty(_settings.Audio.DeviceId))
        {
            MicrophoneCombo.SelectedIndex = 0;
        }
        else
        {
            // Find device by ID
            var index = _audioDevices.FindIndex(d => d.Id == _settings.Audio.DeviceId);
            if (index >= 0)
            {
                MicrophoneCombo.SelectedIndex = index + 1; // +1 for "Default" option
            }
            else
            {
                MicrophoneCombo.SelectedIndex = 0;
            }
        }
    }
    
    private void UpdateHotkeyDisplay()
    {
        var parts = new List<string>();
        
        if (_settings.Hotkey.UseControl) parts.Add("Ctrl");
        if (_settings.Hotkey.UseAlt) parts.Add("Alt");
        if (_settings.Hotkey.UseShift) parts.Add("Shift");
        if (_settings.Hotkey.UseWin) parts.Add("Win");
        
        // Convert virtual key code to key name
        var key = KeyInterop.KeyFromVirtualKey(_settings.Hotkey.VirtualKeyCode);
        parts.Add(key.ToString());
        
        HotkeyTextBox.Text = string.Join(" + ", parts);
    }
    
    private void HotkeyTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        _isCapturingHotkey = true;
        HotkeyTextBox.Text = "Press your hotkey...";
        HotkeyTextBox.PreviewKeyDown += HotkeyTextBox_PreviewKeyDown;
    }
    
    private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isCapturingHotkey) return;
        
        e.Handled = true;
        
        // Ignore modifier-only key presses
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
            e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
            e.Key == Key.LeftShift || e.Key == Key.RightShift ||
            e.Key == Key.LWin || e.Key == Key.RWin)
        {
            return;
        }
        
        _capturedKey = e.Key;
        _capturedModifiers = Keyboard.Modifiers;
        
        // Update display
        var parts = new List<string>();
        if (_capturedModifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (_capturedModifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (_capturedModifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (_capturedModifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(_capturedKey.ToString());
        
        HotkeyTextBox.Text = string.Join(" + ", parts);
        
        // Update settings
        _settings.Hotkey.UseControl = _capturedModifiers.HasFlag(ModifierKeys.Control);
        _settings.Hotkey.UseAlt = _capturedModifiers.HasFlag(ModifierKeys.Alt);
        _settings.Hotkey.UseShift = _capturedModifiers.HasFlag(ModifierKeys.Shift);
        _settings.Hotkey.UseWin = _capturedModifiers.HasFlag(ModifierKeys.Windows);
        _settings.Hotkey.VirtualKeyCode = KeyInterop.VirtualKeyFromKey(_capturedKey);
        
        _isCapturingHotkey = false;
        HotkeyTextBox.PreviewKeyDown -= HotkeyTextBox_PreviewKeyDown;
        
        // Move focus away
        Keyboard.ClearFocus();
    }
    
    private void AsrProviderCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (AsrProviderCombo.SelectedIndex == 0)
        {
            AsrApiKeyPanel.Visibility = Visibility.Visible;
            AsrLocalPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            AsrApiKeyPanel.Visibility = Visibility.Collapsed;
            AsrLocalPanel.Visibility = Visibility.Visible;
        }
    }
    
    private void LlmProviderCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (LlmProviderCombo.SelectedIndex == 0)
        {
            LlmApiKeyPanel.Visibility = Visibility.Visible;
            LlmLocalPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            LlmApiKeyPanel.Visibility = Visibility.Collapsed;
            LlmLocalPanel.Visibility = Visibility.Visible;
        }
    }
    
    private void BrowseAsrModel_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Whisper ONNX Model Folder"
        };
        
        if (dialog.ShowDialog() == true)
        {
            AsrModelPathTextBox.Text = dialog.FolderName;
        }
    }
    
    private void BrowseLlmModel_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Phi-3 ONNX Model Folder"
        };
        
        if (dialog.ShowDialog() == true)
        {
            LlmModelPathTextBox.Text = dialog.FolderName;
        }
    }
    
    private async void DownloadWhisperModel_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "This will download the Whisper small model (~500MB) from Hugging Face.\n\n" +
            "The model will be saved to 'models/whisper-small-onnx' folder.\n\n" +
            "Proceed with download?",
            "Download Whisper Model",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        
        if (result != MessageBoxResult.Yes) return;
        
        // Open HuggingFace page for now
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://huggingface.co/onnx-community/whisper-small",
            UseShellExecute = true
        });
        
        MessageBox.Show(
            "Please download the ONNX model files from Hugging Face and place them in:\n\n" +
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "whisper-small-onnx"),
            "Download Instructions",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
    
    private async void DownloadPhi3Model_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "This will open the Phi-3 Mini ONNX model page.\n\n" +
            "The model will be saved to 'models/phi3-mini-onnx' folder.\n\n" +
            "Proceed?",
            "Download Phi-3 Model",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        
        if (result != MessageBoxResult.Yes) return;
        
        // Open Microsoft/HuggingFace page for Phi-3
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-onnx",
            UseShellExecute = true
        });
        
        MessageBox.Show(
            "Please download the ONNX model files and place them in:\n\n" +
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "phi3-mini-onnx"),
            "Download Instructions",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
    
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
    
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // Update ASR settings
        _settings.Asr.Provider = AsrProviderCombo.SelectedIndex == 0 ? "Cloud" : "Local";
        _settings.Asr.OpenAiApiKey = AsrApiKeyTextBox.Text;
        _settings.Asr.LocalModelPath = AsrModelPathTextBox.Text;
        
        // Update LLM settings
        _settings.Llm.EnableEnhancement = EnableEnhancementCheckBox.IsChecked ?? true;
        _settings.Llm.Provider = LlmProviderCombo.SelectedIndex == 0 ? "Cloud" : "Local";
        _settings.Llm.OpenAiApiKey = LlmApiKeyTextBox.Text;
        _settings.Llm.LocalModelPath = LlmModelPathTextBox.Text;
        
        // Update audio settings
        _settings.Asr.UseGpu = UseGpuCheckBox.IsChecked ?? true;
        _settings.Llm.UseGpu = UseGpuCheckBox.IsChecked ?? true;
        
        if (MicrophoneCombo.SelectedIndex > 0 && MicrophoneCombo.SelectedIndex <= _audioDevices.Count)
        {
            // Use actual device ID, not friendly name
            _settings.Audio.DeviceId = _audioDevices[MicrophoneCombo.SelectedIndex - 1].Id;
        }
        else
        {
            _settings.Audio.DeviceId = null; // Use default
        }
        
        // Save to file
        try
        {
            SaveSettings();
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to save settings: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
    
    private void SaveSettings()
    {
        var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        
        var json = JsonSerializer.Serialize(_settings, options);
        File.WriteAllText(settingsPath, json);
    }
}

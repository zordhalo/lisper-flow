import './settings.css';

// DOM Elements
const form = document.getElementById('settingsForm') as HTMLFormElement;
const deepgramApiKeyInput = document.getElementById('deepgramApiKey') as HTMLInputElement;
const llmApiKeyInput = document.getElementById('llmApiKey') as HTMLInputElement;
const cleanupEnabledInput = document.getElementById('cleanupEnabled') as HTMLInputElement;
const modePasteInput = document.getElementById('modePaste') as HTMLInputElement;
const modeTypeInput = document.getElementById('modeType') as HTMLInputElement;
const statusIndicator = document.getElementById('statusIndicator') as HTMLElement;
const statusText = document.getElementById('statusText') as HTMLElement;
const transcriptBox = document.getElementById('transcriptBox') as HTMLElement;
const deepgramLink = document.getElementById('deepgramLink') as HTMLAnchorElement;

// Prevent default link behavior and copy URL
deepgramLink.addEventListener('click', (e) => {
  e.preventDefault();
  // Can't open external links in Electron easily, so just show the URL
  alert('Visit: https://console.deepgram.com');
});

// Load settings on page load
async function loadSettings(): Promise<void> {
  try {
    const config = await window.electronAPI.getConfig();
    console.log('Loaded config:', config);

    deepgramApiKeyInput.value = config.deepgramApiKey || '';
    llmApiKeyInput.value = config.llmApiKey || '';
    cleanupEnabledInput.checked = config.cleanupEnabled ?? true;

    if (config.insertionMode === 'type') {
      modeTypeInput.checked = true;
      modePasteInput.checked = false;
    } else {
      modePasteInput.checked = true;
      modeTypeInput.checked = false;
    }
  } catch (error) {
    console.error('Failed to load settings:', error);
  }
}

// Save settings on form submit
form.addEventListener('submit', async (e) => {
  e.preventDefault();
  e.stopPropagation();

  const config = {
    deepgramApiKey: deepgramApiKeyInput.value.trim(),
    llmApiKey: llmApiKeyInput.value.trim(),
    cleanupEnabled: cleanupEnabledInput.checked,
    insertionMode: modeTypeInput.checked ? 'type' as const : 'paste' as const,
  };

  console.log('Saving config:', config);

  try {
    const savedConfig = await window.electronAPI.setConfig(config);
    console.log('Saved config returned:', savedConfig);
    showStatus('Settings saved!', 'success');
  } catch (error) {
    console.error('Failed to save settings:', error);
    showStatus('Failed to save settings', 'error');
  }

  return false;
});

function showStatus(message: string, type: 'success' | 'error'): void {
  const originalText = statusText.textContent;
  statusText.textContent = message;
  statusText.style.color = type === 'success' ? '#4ade80' : '#f87171';

  setTimeout(() => {
    statusText.textContent = originalText;
    statusText.style.color = '';
  }, 2000);
}

function updateRecordingState(state: 'idle' | 'recording' | 'processing' | 'error'): void {
  statusIndicator.className = 'status-indicator';

  switch (state) {
    case 'recording':
      statusIndicator.classList.add('recording');
      statusText.textContent = 'Recording...';
      break;
    case 'processing':
      statusIndicator.classList.add('processing');
      statusText.textContent = 'Processing...';
      break;
    case 'error':
      statusIndicator.classList.add('error');
      statusText.textContent = 'Error occurred';
      break;
    default:
      statusText.textContent = 'Idle - Hold Left Ctrl to dictate';
  }
}

function updateTranscript(text: string, isFinal: boolean): void {
  if (!text) {
    transcriptBox.innerHTML = '<p class="placeholder">Your transcribed text will appear here...</p>';
    return;
  }

  const className = isFinal ? 'final' : 'partial';
  transcriptBox.innerHTML = `<p class="${className}">${escapeHtml(text)}</p>`;
}

function escapeHtml(text: string): string {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}

// Set up IPC listeners
window.electronAPI.onRecordingStateChanged((state) => {
  updateRecordingState(state as 'idle' | 'recording' | 'processing' | 'error');
  if (state === 'recording') {
    updateTranscript('', false);
  }
});

window.electronAPI.onPartialTranscript((text) => {
  updateTranscript(text, false);
});

window.electronAPI.onFinalTranscript((text) => {
  updateTranscript(text, true);
});

window.electronAPI.onError((error) => {
  updateRecordingState('error');
  console.error('Error:', error);

  setTimeout(() => {
    updateRecordingState('idle');
  }, 3000);
});

// Initialize
loadSettings();

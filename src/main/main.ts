import { app, BrowserWindow, ipcMain, screen } from 'electron';
import * as path from 'path';
import { hotkeyHandler } from './hotkey';
import { deepgramHandler } from './deepgram';
import { cleanTranscript } from './llm';
import { insertText } from './textInsertion';
import { trayManager } from './tray';
import { configStore } from '../shared/config';
import { IPC_CHANNELS } from '../shared/ipc-channels';
import { RecordingState, AppConfig } from '../shared/types';

declare const AUDIO_WINDOW_WEBPACK_ENTRY: string;
declare const AUDIO_WINDOW_PRELOAD_WEBPACK_ENTRY: string;
declare const SETTINGS_WINDOW_WEBPACK_ENTRY: string;
declare const SETTINGS_WINDOW_PRELOAD_WEBPACK_ENTRY: string;
declare const OVERLAY_WINDOW_WEBPACK_ENTRY: string;
declare const OVERLAY_WINDOW_PRELOAD_WEBPACK_ENTRY: string;

let audioWindow: BrowserWindow | null = null;
let settingsWindow: BrowserWindow | null = null;
let overlayWindow: BrowserWindow | null = null;
let isQuitting = false;

function createAudioWindow(): void {
  audioWindow = new BrowserWindow({
    show: false,
    webPreferences: {
      preload: AUDIO_WINDOW_PRELOAD_WEBPACK_ENTRY,
      nodeIntegration: false,
      contextIsolation: true,
    },
  });

  audioWindow.loadURL(AUDIO_WINDOW_WEBPACK_ENTRY);

  audioWindow.on('closed', () => {
    audioWindow = null;
  });
}

function createSettingsWindow(): void {
  settingsWindow = new BrowserWindow({
    width: 500,
    height: 500,
    show: false,
    resizable: false,
    title: 'LisperFlow Settings',
    webPreferences: {
      preload: SETTINGS_WINDOW_PRELOAD_WEBPACK_ENTRY,
      nodeIntegration: false,
      contextIsolation: true,
    },
  });

  settingsWindow.loadURL(SETTINGS_WINDOW_WEBPACK_ENTRY);

  // Hide instead of close
  settingsWindow.on('close', (event) => {
    if (!isQuitting) {
      event.preventDefault();
      settingsWindow?.hide();
    }
  });

  settingsWindow.on('closed', () => {
    settingsWindow = null;
  });
}

function createOverlayWindow(): void {
  const primaryDisplay = screen.getPrimaryDisplay();
  const { width } = primaryDisplay.workAreaSize;

  overlayWindow = new BrowserWindow({
    width: 80,
    height: 80,
    x: width - 100, // 20px from right edge
    y: 20, // 20px from top
    transparent: true,
    frame: false,
    alwaysOnTop: true,
    skipTaskbar: true,
    resizable: false,
    focusable: false,
    show: false,
    webPreferences: {
      preload: OVERLAY_WINDOW_PRELOAD_WEBPACK_ENTRY,
      nodeIntegration: false,
      contextIsolation: true,
    },
  });

  overlayWindow.loadURL(OVERLAY_WINDOW_WEBPACK_ENTRY);
  overlayWindow.setIgnoreMouseEvents(true);

  overlayWindow.on('closed', () => {
    overlayWindow = null;
  });
}

function showOverlay(): void {
  if (overlayWindow && !overlayWindow.isDestroyed()) {
    overlayWindow.show();
  }
}

function hideOverlay(): void {
  if (overlayWindow && !overlayWindow.isDestroyed()) {
    overlayWindow.hide();
  }
}

function setupIpcHandlers(): void {
  // Settings handlers
  ipcMain.handle(IPC_CHANNELS.SETTINGS_GET, () => {
    const config = configStore.getAll();
    console.log('[IPC] SETTINGS_GET returning:', {
      ...config,
      deepgramApiKey: config.deepgramApiKey ? `***${config.deepgramApiKey.slice(-4)}` : '(empty)',
      llmApiKey: config.llmApiKey ? `***${config.llmApiKey.slice(-4)}` : '(empty)',
    });
    return config;
  });

  ipcMain.handle(IPC_CHANNELS.SETTINGS_SET, (_, config: Partial<AppConfig>) => {
    console.log('[IPC] SETTINGS_SET received:', {
      ...config,
      deepgramApiKey: config.deepgramApiKey ? `***${config.deepgramApiKey.slice(-4)}` : '(empty)',
      llmApiKey: config.llmApiKey ? `***${config.llmApiKey.slice(-4)}` : '(empty)',
    });
    configStore.setAll(config);
    const savedConfig = configStore.getAll();
    console.log('[IPC] SETTINGS_SET saved, returning:', {
      ...savedConfig,
      deepgramApiKey: savedConfig.deepgramApiKey ? `***${savedConfig.deepgramApiKey.slice(-4)}` : '(empty)',
      llmApiKey: savedConfig.llmApiKey ? `***${savedConfig.llmApiKey.slice(-4)}` : '(empty)',
    });
    return savedConfig;
  });

  // Audio chunk handler
  ipcMain.on(IPC_CHANNELS.AUDIO_CHUNK, (_, chunk: ArrayBuffer) => {
    const buffer = Buffer.from(chunk);
    deepgramHandler.sendAudio(buffer);
  });
}

async function handleRecordingStart(): Promise<void> {
  console.log('Recording started');

  // Check for API key
  if (!configStore.hasDeepgramKey()) {
    trayManager.showError('Deepgram API key not configured');
    hideOverlay();
    settingsWindow?.webContents.send(IPC_CHANNELS.RECORDING_STATE_CHANGED, 'error');
    settingsWindow?.show();
    return;
  }

  trayManager.setState(RecordingState.Recording);

  // Notify settings window of recording state
  settingsWindow?.webContents.send(IPC_CHANNELS.RECORDING_STATE_CHANGED, 'recording');

  try {
    // Start Deepgram stream
    await deepgramHandler.startStream();

    // Tell renderer to start capturing audio
    audioWindow?.webContents.send(IPC_CHANNELS.START_RECORDING);

    // Send partial transcripts to settings window
    deepgramHandler.onPartial((text) => {
      settingsWindow?.webContents.send(IPC_CHANNELS.TRANSCRIPT_PARTIAL, text);
    });
  } catch (error) {
    console.error('Failed to start recording:', error);
    trayManager.showError('Failed to start recording');
    settingsWindow?.webContents.send(IPC_CHANNELS.RECORDING_STATE_CHANGED, 'error');
  }
}

async function handleRecordingStop(): Promise<void> {
  console.log('Recording stopped');

  // Hide overlay when recording stops
  hideOverlay();

  trayManager.setState(RecordingState.Processing);

  // Notify settings window of processing state
  settingsWindow?.webContents.send(IPC_CHANNELS.RECORDING_STATE_CHANGED, 'processing');

  // Tell renderer to stop capturing audio
  audioWindow?.webContents.send(IPC_CHANNELS.STOP_RECORDING);

  try {
    // Get final transcript from Deepgram
    const rawTranscript = await deepgramHandler.stopStream();
    console.log('Raw transcript:', rawTranscript);

    if (!rawTranscript) {
      console.log('No transcript received');
      trayManager.setState(RecordingState.Idle);
      settingsWindow?.webContents.send(IPC_CHANNELS.RECORDING_STATE_CHANGED, 'idle');
      return;
    }

    // Clean up with LLM if enabled
    let finalText = rawTranscript;
    if (configStore.get('cleanupEnabled') && configStore.hasLlmKey()) {
      try {
        finalText = await cleanTranscript(rawTranscript);
        console.log('Cleaned transcript:', finalText);
      } catch (error) {
        console.error('LLM cleanup failed, using raw transcript:', error);
      }
    }

    // Insert text into focused application
    await insertText(finalText);

    // Send final transcript to settings window
    settingsWindow?.webContents.send(IPC_CHANNELS.TRANSCRIPT_FINAL, finalText);

    trayManager.setState(RecordingState.Idle);
    settingsWindow?.webContents.send(IPC_CHANNELS.RECORDING_STATE_CHANGED, 'idle');
  } catch (error) {
    console.error('Error processing transcript:', error);
    trayManager.showError('Error processing transcript');
    settingsWindow?.webContents.send(IPC_CHANNELS.RECORDING_STATE_CHANGED, 'error');
  }
}

function setupHotkeyHandlers(): void {
  console.log('Setting up hotkey handlers...');

  // Show overlay immediately on key press (before debounce)
  hotkeyHandler.on('keyPressed', () => {
    console.log('>>> KEY PRESSED - showing overlay');
    showOverlay();
  });

  // Hide overlay if key released before recording started (quick tap)
  hotkeyHandler.on('keyReleased', () => {
    console.log('>>> KEY RELEASED (quick tap) - hiding overlay');
    hideOverlay();
  });

  hotkeyHandler.on('recordingStart', () => {
    console.log('>>> RECORDING START event received');
    handleRecordingStart();
  });

  hotkeyHandler.on('recordingStop', () => {
    console.log('>>> RECORDING STOP event received');
    handleRecordingStop();
  });

  hotkeyHandler.start();
  console.log('Hotkey handlers setup complete');
}

app.on('ready', () => {
  console.log('=== APP READY ===');

  console.log('Creating audio window...');
  createAudioWindow();

  console.log('Creating settings window...');
  createSettingsWindow();

  console.log('Creating overlay window...');
  createOverlayWindow();

  console.log('Initializing tray...');
  trayManager.init(settingsWindow);

  console.log('Setting up IPC handlers...');
  setupIpcHandlers();

  console.log('Setting up hotkey handlers...');
  setupHotkeyHandlers();

  // Show settings on first run if no API keys configured
  if (!configStore.hasDeepgramKey()) {
    console.log('No Deepgram key configured, showing settings');
    settingsWindow?.show();
  }

  console.log('=== LisperFlow started successfully ===');
});

app.on('window-all-closed', () => {
  // Don't quit when all windows are closed (tray app)
});

app.on('before-quit', () => {
  isQuitting = true;
  hotkeyHandler.stop();
  trayManager.destroy();

  // Clean up overlay window
  if (overlayWindow && !overlayWindow.isDestroyed()) {
    overlayWindow.close();
    overlayWindow = null;
  }
});

app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) {
    createAudioWindow();
    createSettingsWindow();
  }
});

// Prevent multiple instances
const gotTheLock = app.requestSingleInstanceLock();
if (!gotTheLock) {
  app.quit();
} else {
  app.on('second-instance', () => {
    if (settingsWindow) {
      if (settingsWindow.isMinimized()) {
        settingsWindow.restore();
      }
      settingsWindow.show();
      settingsWindow.focus();
    }
  });
}

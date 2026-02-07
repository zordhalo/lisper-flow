import { contextBridge, ipcRenderer, IpcRendererEvent } from 'electron';
import { IPC_CHANNELS } from '../shared/ipc-channels';
import { AppConfig } from '../shared/types';

// Type-safe API exposed to renderer
export interface ElectronAPI {
  // Settings
  getConfig: () => Promise<AppConfig>;
  setConfig: (config: Partial<AppConfig>) => Promise<AppConfig>;

  // Audio
  sendAudioChunk: (chunk: ArrayBuffer) => void;

  // Listeners
  onStartRecording: (callback: () => void) => () => void;
  onStopRecording: (callback: () => void) => () => void;
  onPartialTranscript: (callback: (text: string) => void) => () => void;
  onFinalTranscript: (callback: (text: string) => void) => () => void;
  onRecordingStateChanged: (callback: (state: string) => void) => () => void;
  onStatusUpdate: (callback: (status: string) => void) => () => void;
  onError: (callback: (error: string) => void) => () => void;
}

const api: ElectronAPI = {
  // Settings
  getConfig: () => ipcRenderer.invoke(IPC_CHANNELS.SETTINGS_GET),
  setConfig: (config: Partial<AppConfig>) =>
    ipcRenderer.invoke(IPC_CHANNELS.SETTINGS_SET, config),

  // Audio
  sendAudioChunk: (chunk: ArrayBuffer) => {
    ipcRenderer.send(IPC_CHANNELS.AUDIO_CHUNK, chunk);
  },

  // Listeners with cleanup functions
  onStartRecording: (callback: () => void) => {
    const handler = () => callback();
    ipcRenderer.on(IPC_CHANNELS.START_RECORDING, handler);
    return () => ipcRenderer.removeListener(IPC_CHANNELS.START_RECORDING, handler);
  },

  onStopRecording: (callback: () => void) => {
    const handler = () => callback();
    ipcRenderer.on(IPC_CHANNELS.STOP_RECORDING, handler);
    return () => ipcRenderer.removeListener(IPC_CHANNELS.STOP_RECORDING, handler);
  },

  onPartialTranscript: (callback: (text: string) => void) => {
    const handler = (_: IpcRendererEvent, text: string) => callback(text);
    ipcRenderer.on(IPC_CHANNELS.TRANSCRIPT_PARTIAL, handler);
    return () => ipcRenderer.removeListener(IPC_CHANNELS.TRANSCRIPT_PARTIAL, handler);
  },

  onFinalTranscript: (callback: (text: string) => void) => {
    const handler = (_: IpcRendererEvent, text: string) => callback(text);
    ipcRenderer.on(IPC_CHANNELS.TRANSCRIPT_FINAL, handler);
    return () => ipcRenderer.removeListener(IPC_CHANNELS.TRANSCRIPT_FINAL, handler);
  },

  onRecordingStateChanged: (callback: (state: string) => void) => {
    const handler = (_: IpcRendererEvent, state: string) => callback(state);
    ipcRenderer.on(IPC_CHANNELS.RECORDING_STATE_CHANGED, handler);
    return () => ipcRenderer.removeListener(IPC_CHANNELS.RECORDING_STATE_CHANGED, handler);
  },

  onStatusUpdate: (callback: (status: string) => void) => {
    const handler = (_: IpcRendererEvent, status: string) => callback(status);
    ipcRenderer.on(IPC_CHANNELS.STATUS_UPDATE, handler);
    return () => ipcRenderer.removeListener(IPC_CHANNELS.STATUS_UPDATE, handler);
  },

  onError: (callback: (error: string) => void) => {
    const handler = (_: IpcRendererEvent, error: string) => callback(error);
    ipcRenderer.on(IPC_CHANNELS.ERROR, handler);
    return () => ipcRenderer.removeListener(IPC_CHANNELS.ERROR, handler);
  },
};

contextBridge.exposeInMainWorld('electronAPI', api);

// Also expose for TypeScript
declare global {
  interface Window {
    electronAPI: ElectronAPI;
  }
}

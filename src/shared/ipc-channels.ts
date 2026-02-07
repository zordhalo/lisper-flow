export const IPC_CHANNELS = {
  // Audio
  AUDIO_CHUNK: 'audio:chunk',
  START_RECORDING: 'audio:start',
  STOP_RECORDING: 'audio:stop',
  RECORDING_STATE_CHANGED: 'audio:state-changed',

  // Settings
  SETTINGS_GET: 'settings:get',
  SETTINGS_SET: 'settings:set',
  SETTINGS_OPEN: 'settings:open',

  // Status
  STATUS_UPDATE: 'status:update',
  ERROR: 'error',

  // Transcript
  TRANSCRIPT_PARTIAL: 'transcript:partial',
  TRANSCRIPT_FINAL: 'transcript:final',
} as const;

export type IpcChannel = typeof IPC_CHANNELS[keyof typeof IPC_CHANNELS];

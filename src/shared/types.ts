export interface AppConfig {
  deepgramApiKey: string;
  llmApiKey: string;
  cleanupEnabled: boolean;
  insertionMode: 'paste' | 'type';
}

export enum RecordingState {
  Idle = 'idle',
  Recording = 'recording',
  Processing = 'processing',
  Error = 'error',
}

export interface AudioChunk {
  data: ArrayBuffer;
  timestamp: number;
}

export interface TranscriptResult {
  text: string;
  isFinal: boolean;
  confidence?: number;
}

export interface DeepgramConfig {
  model: string;
  language: string;
  smartFormat: boolean;
  punctuate: boolean;
}

export const DEFAULT_CONFIG: AppConfig = {
  deepgramApiKey: '',
  llmApiKey: '',
  cleanupEnabled: true,
  insertionMode: 'paste',
};

export const DEFAULT_DEEPGRAM_CONFIG: DeepgramConfig = {
  model: 'nova-2',
  language: 'en-US',
  smartFormat: true,
  punctuate: true,
};

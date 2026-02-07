import { createClient, LiveTranscriptionEvents, LiveClient } from '@deepgram/sdk';
import { configStore } from '../shared/config';
import { DEFAULT_DEEPGRAM_CONFIG, TranscriptResult } from '../shared/types';

class DeepgramHandler {
  private connection: LiveClient | null = null;
  private transcriptParts: string[] = [];
  private onPartialCallback: ((text: string) => void) | null = null;
  private resolveTranscript: ((text: string) => void) | null = null;
  private rejectTranscript: ((error: Error) => void) | null = null;

  async startStream(): Promise<void> {
    const apiKey = configStore.get('deepgramApiKey');
    if (!apiKey) {
      throw new Error('Deepgram API key not configured');
    }

    this.transcriptParts = [];
    const deepgram = createClient(apiKey);

    const config = DEFAULT_DEEPGRAM_CONFIG;

    this.connection = deepgram.listen.live({
      model: config.model,
      language: config.language,
      smart_format: config.smartFormat,
      punctuate: config.punctuate,
      // Don't specify encoding - let Deepgram auto-detect WebM/Opus container format
    });

    return new Promise((resolve, reject) => {
      if (!this.connection) {
        reject(new Error('Failed to create Deepgram connection'));
        return;
      }

      this.connection.on(LiveTranscriptionEvents.Open, () => {
        console.log('Deepgram connection opened');
        resolve();
      });

      this.connection.on(LiveTranscriptionEvents.Transcript, (data) => {
        const transcript = data.channel?.alternatives?.[0]?.transcript;
        if (transcript) {
          if (data.is_final) {
            this.transcriptParts.push(transcript);
          }

          // Send partial updates
          if (this.onPartialCallback) {
            const currentText = data.is_final
              ? this.transcriptParts.join(' ')
              : [...this.transcriptParts, transcript].join(' ');
            this.onPartialCallback(currentText);
          }
        }
      });

      this.connection.on(LiveTranscriptionEvents.Error, (error) => {
        console.error('Deepgram error:', error);
        if (this.rejectTranscript) {
          this.rejectTranscript(new Error(`Deepgram error: ${error.message || error}`));
        }
      });

      this.connection.on(LiveTranscriptionEvents.Close, () => {
        console.log('Deepgram connection closed');
        if (this.resolveTranscript) {
          this.resolveTranscript(this.transcriptParts.join(' ').trim());
        }
      });

      // Set a timeout for connection
      setTimeout(() => {
        if (this.connection?.getReadyState() !== 1) {
          reject(new Error('Deepgram connection timeout'));
        }
      }, 5000);
    });
  }

  sendAudio(audioData: Buffer): void {
    if (this.connection && this.connection.getReadyState() === 1) {
      // Convert Buffer to ArrayBuffer for Deepgram WebSocket
      const arrayBuffer = audioData.buffer.slice(
        audioData.byteOffset,
        audioData.byteOffset + audioData.byteLength
      );
      this.connection.send(arrayBuffer);
    }
  }

  async stopStream(): Promise<string> {
    return new Promise((resolve, reject) => {
      if (!this.connection) {
        resolve(this.transcriptParts.join(' ').trim());
        return;
      }

      this.resolveTranscript = resolve;
      this.rejectTranscript = reject;

      // Set a timeout in case close event doesn't fire
      const timeout = setTimeout(() => {
        const text = this.transcriptParts.join(' ').trim();
        this.cleanup();
        resolve(text);
      }, 3000);

      const originalResolve = this.resolveTranscript;
      this.resolveTranscript = (text: string) => {
        clearTimeout(timeout);
        this.cleanup();
        originalResolve(text);
      };

      this.connection.requestClose();
    });
  }

  onPartial(callback: (text: string) => void): void {
    this.onPartialCallback = callback;
  }

  private cleanup(): void {
    this.connection = null;
    this.resolveTranscript = null;
    this.rejectTranscript = null;
    this.onPartialCallback = null;
  }

  isConnected(): boolean {
    return this.connection !== null && this.connection.getReadyState() === 1;
  }
}

export const deepgramHandler = new DeepgramHandler();

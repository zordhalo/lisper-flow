import { GlobalKeyboardListener, IGlobalKeyEvent } from 'node-global-key-listener';
import { EventEmitter } from 'events';

const DEBOUNCE_THRESHOLD_MS = 100;

export interface HotkeyEvents {
  recordingStart: () => void;
  recordingStop: () => void;
}

class HotkeyHandler extends EventEmitter {
  private keyListener: GlobalKeyboardListener | null = null;
  private isCtrlPressed = false;
  private lastKeyDownTime = 0;
  private isRecording = false;

  constructor() {
    super();
  }

  start(): void {
    if (this.keyListener) {
      return;
    }

    this.keyListener = new GlobalKeyboardListener();

    this.keyListener.addListener((event: IGlobalKeyEvent) => {
      this.handleKeyEvent(event);
    });

    console.log('Hotkey listener started');
  }

  stop(): void {
    if (this.keyListener) {
      this.keyListener.kill();
      this.keyListener = null;
    }
    this.isCtrlPressed = false;
    this.isRecording = false;
    console.log('Hotkey listener stopped');
  }

  private handleKeyEvent(event: IGlobalKeyEvent): void {
    // Only listen for LEFT CTRL
    if (event.name !== 'LEFT CTRL') {
      return;
    }

    const now = Date.now();

    if (event.state === 'DOWN') {
      if (!this.isCtrlPressed) {
        this.isCtrlPressed = true;
        this.lastKeyDownTime = now;
      }
    } else if (event.state === 'UP') {
      if (this.isCtrlPressed) {
        this.isCtrlPressed = false;
        const holdDuration = now - this.lastKeyDownTime;

        // Debounce: ignore quick taps
        if (holdDuration < DEBOUNCE_THRESHOLD_MS) {
          if (this.isRecording) {
            // If we were recording (from a previous longer hold), stop it
            this.isRecording = false;
            this.emit('recordingStop');
          }
          return;
        }

        // If we were recording, stop
        if (this.isRecording) {
          this.isRecording = false;
          this.emit('recordingStop');
        }
      }
    }

    // Start recording after debounce threshold while key is held
    if (this.isCtrlPressed && !this.isRecording) {
      const holdDuration = Date.now() - this.lastKeyDownTime;
      if (holdDuration >= DEBOUNCE_THRESHOLD_MS) {
        this.isRecording = true;
        this.emit('recordingStart');
      } else {
        // Check again after the threshold
        setTimeout(() => {
          if (this.isCtrlPressed && !this.isRecording) {
            this.isRecording = true;
            this.emit('recordingStart');
          }
        }, DEBOUNCE_THRESHOLD_MS - holdDuration);
      }
    }
  }

  getIsRecording(): boolean {
    return this.isRecording;
  }
}

export const hotkeyHandler = new HotkeyHandler();

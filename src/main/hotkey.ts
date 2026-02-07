import { GlobalKeyboardListener, IGlobalKeyEvent } from 'node-global-key-listener';
import { EventEmitter } from 'events';

const DEBOUNCE_THRESHOLD_MS = 50; // Reduced for better responsiveness

export interface HotkeyEvents {
  keyPressed: () => void; // Immediate feedback on key press
  keyReleased: () => void; // Key released without recording (quick tap)
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

        // Emit immediate feedback event (before actual recording starts)
        this.emit('keyPressed');

        // Schedule recording start after debounce threshold (only once on key down)
        setTimeout(() => {
          if (this.isCtrlPressed && !this.isRecording) {
            this.isRecording = true;
            this.emit('recordingStart');
          }
        }, DEBOUNCE_THRESHOLD_MS);
      }
    } else if (event.state === 'UP') {
      if (this.isCtrlPressed) {
        this.isCtrlPressed = false;

        // If we were recording, stop
        if (this.isRecording) {
          this.isRecording = false;
          this.emit('recordingStop');
        } else {
          // Key released before recording started (quick tap)
          this.emit('keyReleased');
        }
      }
    }
  }

  getIsRecording(): boolean {
    return this.isRecording;
  }
}

export const hotkeyHandler = new HotkeyHandler();

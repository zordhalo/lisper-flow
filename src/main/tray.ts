import { Tray, Menu, nativeImage, app, BrowserWindow } from 'electron';
import * as path from 'path';
import { RecordingState } from '../shared/types';

class TrayManager {
  private tray: Tray | null = null;
  private currentState: RecordingState = RecordingState.Idle;
  private settingsWindow: BrowserWindow | null = null;

  private iconIdle: Electron.NativeImage | null = null;
  private iconRecording: Electron.NativeImage | null = null;

  init(settingsWindow: BrowserWindow | null): void {
    this.settingsWindow = settingsWindow;
    this.loadIcons();
    this.createTray();
  }

  private loadIcons(): void {
    try {
      // Determine the correct assets path based on environment
      let assetsPath: string;

      if (app.isPackaged) {
        // In production, assets are in resources/assets (from extraResource)
        assetsPath = path.join(process.resourcesPath, 'assets');
      } else {
        // In development, assets are in project root
        assetsPath = path.join(app.getAppPath(), 'assets');
      }

      const idlePath = path.join(assetsPath, 'icon.png');
      const recordingPath = path.join(assetsPath, 'icon-recording.png');

      this.iconIdle = nativeImage.createFromPath(idlePath);
      this.iconRecording = nativeImage.createFromPath(recordingPath);

      if (!this.iconIdle.isEmpty()) {
        console.log('Icons loaded from:', assetsPath);
      } else {
        console.warn('Icons are empty, using fallback');
        this.createFallbackIcons();
      }
    } catch (error) {
      console.error('Error loading tray icons:', error);
      this.createFallbackIcons();
    }
  }

  private createFallbackIcons(): void {
    // Create simple colored icons as fallback (16x16 solid squares)
    const createColoredIcon = (r: number, g: number, b: number): Electron.NativeImage => {
      // Create a simple 16x16 PNG with a solid color
      const size = 16;
      const channels = 4; // RGBA
      const buffer = Buffer.alloc(size * size * channels);

      for (let i = 0; i < size * size; i++) {
        buffer[i * channels] = r;
        buffer[i * channels + 1] = g;
        buffer[i * channels + 2] = b;
        buffer[i * channels + 3] = 255;
      }

      return nativeImage.createFromBuffer(buffer, { width: size, height: size });
    };

    this.iconIdle = createColoredIcon(107, 114, 128); // Gray
    this.iconRecording = createColoredIcon(239, 68, 68); // Red
  }

  private createTray(): void {
    if (!this.iconIdle) {
      return;
    }

    // Resize icon for tray (16x16 on Windows)
    const trayIcon = this.iconIdle.resize({ width: 16, height: 16 });
    this.tray = new Tray(trayIcon);

    this.tray.setToolTip('LisperFlow - Idle');
    this.updateContextMenu();

    this.tray.on('click', () => {
      this.openSettings();
    });
  }

  private updateContextMenu(): void {
    if (!this.tray) return;

    const contextMenu = Menu.buildFromTemplate([
      {
        label: 'Open Settings',
        click: () => this.openSettings(),
      },
      { type: 'separator' },
      {
        label: 'Quit',
        click: () => {
          app.quit();
        },
      },
    ]);

    this.tray.setContextMenu(contextMenu);
  }

  private openSettings(): void {
    if (this.settingsWindow) {
      if (this.settingsWindow.isMinimized()) {
        this.settingsWindow.restore();
      }
      this.settingsWindow.show();
      this.settingsWindow.focus();
    }
  }

  setState(state: RecordingState): void {
    console.log('Tray setState:', state);
    if (!this.tray) {
      console.warn('Tray not initialized');
      return;
    }

    this.currentState = state;

    let tooltip: string;
    let icon: Electron.NativeImage | null;

    switch (state) {
      case RecordingState.Recording:
        tooltip = 'LisperFlow - Recording...';
        icon = this.iconRecording;
        break;
      case RecordingState.Processing:
        tooltip = 'LisperFlow - Processing...';
        icon = this.iconRecording;
        break;
      case RecordingState.Error:
        tooltip = 'LisperFlow - Error';
        icon = this.iconIdle;
        break;
      default:
        tooltip = 'LisperFlow - Idle (Hold Left Ctrl to dictate)';
        icon = this.iconIdle;
    }

    this.tray.setToolTip(tooltip);

    if (icon && !icon.isEmpty()) {
      this.tray.setImage(icon.resize({ width: 16, height: 16 }));
    }
  }

  setSettingsWindow(window: BrowserWindow | null): void {
    this.settingsWindow = window;
  }

  showError(message: string): void {
    if (this.tray) {
      this.tray.setToolTip(`LisperFlow - Error: ${message}`);
    }
    this.setState(RecordingState.Error);

    // Reset to idle after 5 seconds
    setTimeout(() => {
      if (this.currentState === RecordingState.Error) {
        this.setState(RecordingState.Idle);
      }
    }, 5000);
  }

  destroy(): void {
    if (this.tray) {
      this.tray.destroy();
      this.tray = null;
    }
  }
}

export const trayManager = new TrayManager();

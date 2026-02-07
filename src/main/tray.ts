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
      // Try different possible paths for the icons
      const possiblePaths = [
        path.join(__dirname, '../../assets'),
        path.join(__dirname, '../../../assets'),
        path.join(__dirname, '../../../../assets'),
        path.join(app.getAppPath(), 'assets'),
        path.join(app.getAppPath(), '../assets'),
        path.join(process.resourcesPath || '', 'assets'),
        path.resolve('./assets'),
      ];

      console.log('Looking for icons in paths:', possiblePaths);
      console.log('__dirname:', __dirname);
      console.log('app.getAppPath():', app.getAppPath());

      for (const basePath of possiblePaths) {
        const idlePath = path.join(basePath, 'icon.png');
        const recordingPath = path.join(basePath, 'icon-recording.png');

        console.log('Trying icon path:', idlePath);

        try {
          this.iconIdle = nativeImage.createFromPath(idlePath);
          this.iconRecording = nativeImage.createFromPath(recordingPath);

          if (!this.iconIdle.isEmpty()) {
            console.log('Icons loaded successfully from:', basePath);
            break;
          }
        } catch (e) {
          console.log('Failed to load from:', basePath, e);
        }
      }

      // Fallback to empty images if icons not found
      if (!this.iconIdle || this.iconIdle.isEmpty()) {
        console.warn('Could not load tray icons, using default empty icons');
        this.iconIdle = nativeImage.createEmpty();
        this.iconRecording = nativeImage.createEmpty();
      }
    } catch (error) {
      console.error('Error loading tray icons:', error);
      this.iconIdle = nativeImage.createEmpty();
      this.iconRecording = nativeImage.createEmpty();
    }
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

const path = require('path');

module.exports = {
  packagerConfig: {
    name: 'LisperFlow',
    executableName: 'lisper-flow',
    icon: './assets/icon',
    asar: true,
    extraResource: ['./assets'],
  },
  rebuildConfig: {},
  makers: [
    {
      name: '@electron-forge/maker-squirrel',
      config: {
        name: 'LisperFlow',
        setupIcon: './assets/icon.ico',
      },
    },
    {
      name: '@electron-forge/maker-zip',
      platforms: ['win32'],
    },
  ],
  plugins: [
    {
      name: '@electron-forge/plugin-webpack',
      config: {
        mainConfig: './webpack.main.config.js',
        renderer: {
          config: './webpack.renderer.config.js',
          entryPoints: [
            {
              html: './src/renderer/settings.html',
              js: './src/renderer/settings.ts',
              name: 'settings_window',
              preload: {
                js: './src/preload/preload.ts',
              },
            },
            {
              html: './src/renderer/audio.html',
              js: './src/renderer/audio.ts',
              name: 'audio_window',
              preload: {
                js: './src/preload/preload.ts',
              },
            },
            {
              html: './src/renderer/overlay.html',
              js: './src/renderer/overlay.ts',
              name: 'overlay_window',
              preload: {
                js: './src/preload/preload.ts',
              },
            },
          ],
        },
      },
    },
  ],
};

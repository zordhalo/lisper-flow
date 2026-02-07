# LisperFlow

A Windows-first voice dictation application that captures audio while holding Left Ctrl, streams to Deepgram for real-time transcription, optionally cleans up the transcript with an LLM, and inserts the text into any focused application.

## Features

- **Push-to-Talk Dictation**: Hold Left Ctrl to record, release to transcribe
- **Real-time Transcription**: Uses Deepgram's nova-2 model for fast, accurate speech-to-text
- **AI Cleanup (Optional)**: Uses GPT-4o-mini to remove filler words and fix grammar
- **Universal Text Insertion**: Works with any application via clipboard paste or keystroke simulation
- **System Tray Integration**: Runs quietly in the background with status indicators
- **Minimal UI**: Simple settings window, stays out of your way

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         LisperFlow                               │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────┐     ┌──────────────┐     ┌──────────────────┐ │
│  │   Hotkey     │────▶│    Audio     │────▶│    Deepgram      │ │
│  │   Handler    │     │   Capture    │     │    Streaming     │ │
│  │ (Left Ctrl)  │     │  (WebM/Opus) │     │    (nova-2)      │ │
│  └──────────────┘     └──────────────┘     └────────┬─────────┘ │
│                                                      │           │
│                                                      ▼           │
│  ┌──────────────┐     ┌──────────────┐     ┌──────────────────┐ │
│  │    Text      │◀────│     LLM      │◀────│    Transcript    │ │
│  │  Insertion   │     │   Cleanup    │     │    Accumulator   │ │
│  │ (Paste/Type) │     │ (gpt-4o-mini)│     │                  │ │
│  └──────────────┘     └──────────────┘     └──────────────────┘ │
│                                                                  │
│  ┌──────────────┐     ┌──────────────┐                          │
│  │  System Tray │     │   Settings   │                          │
│  │    Icon      │     │    Window    │                          │
│  └──────────────┘     └──────────────┘                          │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

## Installation

### Prerequisites

- Node.js 18+
- Windows 10/11 (primary target)
- Deepgram API key (required)
- OpenAI API key (optional, for AI cleanup)

### Development Setup

1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/lisper-flow.git
   cd lisper-flow
   ```

2. Install dependencies:
   ```bash
   npm install
   ```

3. Start the application:
   ```bash
   npm start
   ```

### Building for Production

```bash
npm run make
```

This creates an installer in the `out/make` directory.

## Configuration

On first launch, the settings window will open. Configure:

1. **Deepgram API Key** (Required)
   - Get your key at [console.deepgram.com](https://console.deepgram.com)
   - Used for speech-to-text transcription

2. **OpenAI API Key** (Optional)
   - Get your key at [platform.openai.com](https://platform.openai.com)
   - Enables AI cleanup of transcripts

3. **Enable AI Cleanup**
   - When enabled, transcripts are processed by GPT-4o-mini to remove filler words, fix grammar, and improve punctuation

4. **Text Insertion Mode**
   - **Paste (Ctrl+V)**: Faster, uses clipboard. Works in most applications.
   - **Type**: Simulates keystrokes character by character. Works in applications that don't support paste.

## Usage

1. Launch LisperFlow (it will minimize to the system tray)
2. Focus on any text input field in any application
3. **Hold Left Ctrl** and speak
4. **Release Left Ctrl** to process and insert the transcribed text

### Tray Icon States

- **Green**: Idle, ready to record
- **Red (pulsing)**: Recording in progress
- **Yellow (pulsing)**: Processing transcript

### Quick Tips

- Speak clearly and at a natural pace
- Short pauses between sentences help with punctuation
- The 100ms debounce prevents accidental activations from quick Ctrl taps
- Check the settings window for live transcript preview

## Technology Stack

| Component | Technology |
|-----------|------------|
| Framework | Electron + Node.js |
| UI | Plain HTML/CSS/JS |
| Hotkey | node-global-key-listener |
| Speech-to-Text | Deepgram Streaming API (nova-2) |
| Text Insertion | @nut-tree/nut-js + Electron clipboard |
| LLM Cleanup | OpenAI Chat Completions (gpt-4o-mini) |
| Config Storage | electron-store |
| Packaging | electron-forge |

## Troubleshooting

### "Deepgram API key not configured"
- Open settings from the tray icon
- Enter your Deepgram API key
- Save settings

### Text not appearing in application
- Try switching between Paste and Type modes in settings
- Ensure the application has focus when you release Ctrl
- Some applications may have paste restrictions

### Recording doesn't start
- Check that no other application is using the microphone
- Grant microphone permissions when prompted
- Try restarting LisperFlow

### Transcript is empty
- Speak louder and closer to the microphone
- Check your Deepgram API key is valid
- Ensure you have sufficient Deepgram credits

## Security & Privacy

- API keys are stored locally using electron-store (encrypted at rest on Windows)
- Audio is streamed directly to Deepgram - not stored locally
- Transcripts are processed in memory and not persisted
- If AI cleanup is enabled, transcripts are sent to OpenAI for processing

## Development

### Project Structure

```
lisper-flow/
├── src/
│   ├── main/           # Main process (Electron)
│   │   ├── main.ts     # Entry point
│   │   ├── hotkey.ts   # Global key listener
│   │   ├── deepgram.ts # Deepgram streaming
│   │   ├── llm.ts      # OpenAI cleanup
│   │   ├── textInsertion.ts
│   │   └── tray.ts     # System tray
│   ├── renderer/       # Renderer process
│   │   ├── settings.*  # Settings window
│   │   └── audio.*     # Audio capture
│   ├── preload/        # Preload scripts
│   │   └── preload.ts  # IPC bridge
│   └── shared/         # Shared code
│       ├── types.ts    # TypeScript types
│       ├── config.ts   # Config store
│       └── ipc-channels.ts
├── assets/             # Icons
└── package.json
```

### Scripts

- `npm start` - Run in development mode
- `npm run make` - Build distributable
- `npm run lint` - Type check with TypeScript

## License

MIT

## Acknowledgments

- [Deepgram](https://deepgram.com) for real-time speech-to-text
- [OpenAI](https://openai.com) for transcript cleanup
- [Electron](https://electronjs.org) for cross-platform desktop app framework

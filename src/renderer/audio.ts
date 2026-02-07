// Audio capture module for hidden window
// Uses raw Linear16 PCM audio for Deepgram streaming

let audioStream: MediaStream | null = null;
let audioContext: AudioContext | null = null;
let sourceNode: MediaStreamAudioSourceNode | null = null;
let processorNode: ScriptProcessorNode | null = null;
let isRecording = false;

const SAMPLE_RATE = 16000;
const BUFFER_SIZE = 4096;

async function initAudio(): Promise<void> {
  try {
    // Request microphone access
    audioStream = await navigator.mediaDevices.getUserMedia({
      audio: {
        echoCancellation: true,
        noiseSuppression: true,
        autoGainControl: true,
        sampleRate: SAMPLE_RATE,
      },
    });
    console.log('Audio stream initialized');
  } catch (error) {
    console.error('Failed to initialize audio:', error);
  }
}

function startRecording(): void {
  if (!audioStream) {
    console.error('Audio stream not initialized');
    return;
  }

  if (isRecording) {
    console.log('Already recording');
    return;
  }

  try {
    // Create AudioContext with target sample rate
    audioContext = new AudioContext({ sampleRate: SAMPLE_RATE });

    // Create source from microphone stream
    sourceNode = audioContext.createMediaStreamSource(audioStream);

    // Create ScriptProcessor for raw PCM capture
    // Note: ScriptProcessor is deprecated but AudioWorklet requires more setup
    processorNode = audioContext.createScriptProcessor(BUFFER_SIZE, 1, 1);

    processorNode.onaudioprocess = (event: AudioProcessingEvent) => {
      if (!isRecording) return;

      const inputData = event.inputBuffer.getChannelData(0);

      // Convert Float32Array (-1.0 to 1.0) to Int16Array (Linear16 PCM)
      const pcmData = new Int16Array(inputData.length);
      for (let i = 0; i < inputData.length; i++) {
        // Clamp and convert to 16-bit signed integer
        const sample = Math.max(-1, Math.min(1, inputData[i]));
        pcmData[i] = sample < 0 ? sample * 0x8000 : sample * 0x7fff;
      }

      // Send PCM data to main process
      window.electronAPI.sendAudioChunk(pcmData.buffer);
    };

    // Connect: microphone -> processor -> destination (required for processing)
    sourceNode.connect(processorNode);
    processorNode.connect(audioContext.destination);

    isRecording = true;
    console.log('Recording started (Linear16 PCM)');
  } catch (error) {
    console.error('Failed to start recording:', error);
    stopRecording();
  }
}

function stopRecording(): void {
  isRecording = false;

  if (processorNode) {
    processorNode.disconnect();
    processorNode = null;
  }

  if (sourceNode) {
    sourceNode.disconnect();
    sourceNode = null;
  }

  if (audioContext) {
    audioContext.close().catch(console.error);
    audioContext = null;
  }

  console.log('Recording stopped');
}

// Set up IPC listeners
window.electronAPI.onStartRecording(() => {
  startRecording();
});

window.electronAPI.onStopRecording(() => {
  stopRecording();
});

// Initialize audio on load
initAudio();

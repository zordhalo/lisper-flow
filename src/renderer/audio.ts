// Audio capture module for hidden window

let mediaRecorder: MediaRecorder | null = null;
let audioStream: MediaStream | null = null;

async function initAudio(): Promise<void> {
  try {
    // Request microphone access
    audioStream = await navigator.mediaDevices.getUserMedia({
      audio: {
        echoCancellation: true,
        noiseSuppression: true,
        autoGainControl: true,
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

  try {
    // Use webm/opus format - Deepgram accepts this directly
    const mimeType = 'audio/webm;codecs=opus';

    if (!MediaRecorder.isTypeSupported(mimeType)) {
      console.error('MIME type not supported:', mimeType);
      return;
    }

    mediaRecorder = new MediaRecorder(audioStream, {
      mimeType,
      audioBitsPerSecond: 128000,
    });

    mediaRecorder.ondataavailable = async (event) => {
      if (event.data.size > 0) {
        // Convert Blob to ArrayBuffer and send to main process
        const arrayBuffer = await event.data.arrayBuffer();
        window.electronAPI.sendAudioChunk(arrayBuffer);
      }
    };

    mediaRecorder.onerror = (event) => {
      console.error('MediaRecorder error:', event);
    };

    // Start recording with 100ms timeslices for streaming
    mediaRecorder.start(100);
    console.log('Recording started');
  } catch (error) {
    console.error('Failed to start recording:', error);
  }
}

function stopRecording(): void {
  if (mediaRecorder && mediaRecorder.state !== 'inactive') {
    mediaRecorder.stop();
    console.log('Recording stopped');
  }
  mediaRecorder = null;
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

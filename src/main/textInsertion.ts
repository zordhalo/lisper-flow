import { clipboard } from 'electron';
import { keyboard, Key } from '@nut-tree-fork/nut-js';
import { configStore } from '../shared/config';

// Small delay to let focus return to the original application
const INSERTION_DELAY_MS = 100;

export async function insertText(text: string): Promise<void> {
  if (!text || text.trim().length === 0) {
    console.log('No text to insert');
    return;
  }

  // Wait for focus to return
  await delay(INSERTION_DELAY_MS);

  const mode = configStore.get('insertionMode');

  try {
    if (mode === 'paste') {
      await insertViaPaste(text);
    } else {
      await insertViaType(text);
    }
    console.log(`Text inserted via ${mode} mode`);
  } catch (error) {
    console.error('Text insertion failed:', error);
    throw error;
  }
}

async function insertViaPaste(text: string): Promise<void> {
  // Store current clipboard content (optional - could restore after)
  // const previousContent = clipboard.readText();

  // Write text to clipboard
  clipboard.writeText(text);

  // Simulate Ctrl+V
  await keyboard.pressKey(Key.LeftControl, Key.V);
  await keyboard.releaseKey(Key.V, Key.LeftControl);

  // Optional: restore previous clipboard content after a delay
  // await delay(100);
  // clipboard.writeText(previousContent);
}

async function insertViaType(text: string): Promise<void> {
  // Type the text character by character
  // This is slower but works in more contexts
  await keyboard.type(text);
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

import Store from 'electron-store';
import { AppConfig, DEFAULT_CONFIG } from './types';

const schema = {
  deepgramApiKey: {
    type: 'string' as const,
    default: '',
  },
  llmApiKey: {
    type: 'string' as const,
    default: '',
  },
  cleanupEnabled: {
    type: 'boolean' as const,
    default: true,
  },
  insertionMode: {
    type: 'string' as const,
    enum: ['paste', 'type'],
    default: 'paste',
  },
};

class ConfigStore {
  private store: Store<AppConfig>;

  constructor() {
    this.store = new Store<AppConfig>({
      name: 'lisper-flow-config',
      schema,
      defaults: DEFAULT_CONFIG,
    });
  }

  get<K extends keyof AppConfig>(key: K): AppConfig[K] {
    return this.store.get(key);
  }

  set<K extends keyof AppConfig>(key: K, value: AppConfig[K]): void {
    this.store.set(key, value);
  }

  getAll(): AppConfig {
    return {
      deepgramApiKey: this.get('deepgramApiKey'),
      llmApiKey: this.get('llmApiKey'),
      cleanupEnabled: this.get('cleanupEnabled'),
      insertionMode: this.get('insertionMode'),
    };
  }

  setAll(config: Partial<AppConfig>): void {
    Object.entries(config).forEach(([key, value]) => {
      if (value !== undefined) {
        this.store.set(key as keyof AppConfig, value);
      }
    });
  }

  hasDeepgramKey(): boolean {
    const key = this.get('deepgramApiKey');
    return typeof key === 'string' && key.length > 0;
  }

  hasLlmKey(): boolean {
    const key = this.get('llmApiKey');
    return typeof key === 'string' && key.length > 0;
  }
}

export const configStore = new ConfigStore();

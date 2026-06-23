import fs from 'fs';
import path from 'path';

const CONFIG_PATH = path.join('data', 'config.json');

export interface FileEntry {
  key: string;
  label: string;
  path: string;
}

export interface AppConfig {
  cv: FileEntry | null;
  letters: FileEntry[];
}

export function loadConfig(): AppConfig {
  try {
    if (fs.existsSync(CONFIG_PATH)) {
      return JSON.parse(fs.readFileSync(CONFIG_PATH, 'utf8'));
    }
  } catch {
    // fall through
  }
  return { cv: null, letters: [] };
}

export function saveConfig(config: AppConfig): void {
  fs.mkdirSync('data', { recursive: true });
  fs.writeFileSync(CONFIG_PATH, JSON.stringify(config, null, 2), 'utf8');
}

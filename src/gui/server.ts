import express from 'express';
import { execFile } from 'child_process';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import open from 'open';
import { loadConfig, saveConfig, AppConfig } from '../fileConfig.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PORT = 3421;

function openFileDialog(filter: string, multiSelect = false): Promise<string[]> {
  return new Promise((resolve) => {
    const script = [
      'Add-Type -AssemblyName System.Windows.Forms',
      '$d = New-Object System.Windows.Forms.OpenFileDialog',
      `$d.Filter = "${filter}"`,
      `$d.Multiselect = $${multiSelect ? 'true' : 'false'}`,
      '$d.InitialDirectory = [Environment]::GetFolderPath("MyDocuments")',
      'if ($d.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {',
      '  if ($d.Multiselect) { $d.FileNames -join "|" } else { $d.FileName }',
      '}',
    ].join('; ');

    // -STA is required for Windows Forms dialogs
    execFile('powershell', ['-STA', '-NoProfile', '-NonInteractive', '-Command', script], (err, stdout, stderr) => {
      if (err) {
        console.error('File dialog error:', stderr || err.message);
        return resolve([]);
      }
      const result = stdout.trim();
      resolve(result ? result.split('|').filter(Boolean) : []);
    });
  });
}

export function startSetupServer(): void {
  const app = express();
  app.use(express.json());

  // Serve the HTML UI
  app.get('/', (_req, res) => {
    res.sendFile(path.join(__dirname, 'app.html'));
  });

  // Load current config
  app.get('/api/config', (_req, res) => {
    res.json(loadConfig());
  });

  // Save config
  app.post('/api/config', (req, res) => {
    saveConfig(req.body as AppConfig);
    res.json({ ok: true });
  });

  // Open native file picker and return selected path(s)
  app.post('/api/browse', async (req, res) => {
    const { filter = 'All Files (*.*)|*.*', multi = false } = req.body as {
      filter?: string;
      multi?: boolean;
    };
    const files = await openFileDialog(filter, multi);
    res.json({ files });
  });

  // Verify a path exists
  app.post('/api/verify-path', (req, res) => {
    const { filePath } = req.body as { filePath: string };
    res.json({ exists: fs.existsSync(filePath) });
  });

  const server = app.listen(PORT, () => {
    const url = `http://localhost:${PORT}`;
    console.log(`\nSetup UI running at ${url}`);
    open(url);
  });

  // Keep alive until user closes the terminal
  process.on('SIGINT', () => {
    server.close();
    process.exit(0);
  });
}

startSetupServer();

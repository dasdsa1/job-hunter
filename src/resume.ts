import fs from 'fs';
import path from 'path';
import chalk from 'chalk';
import inquirer from 'inquirer';
import mammoth from 'mammoth';
import { createRequire } from 'module';
import { loadConfig } from './fileConfig.js';

const require = createRequire(import.meta.url);
const pdfParse = require('pdf-parse') as (buffer: Buffer) => Promise<{ text: string }>;

export async function collectResume(): Promise<string> {
  const config = loadConfig();

  if (config.cv) {
    if (!fs.existsSync(config.cv.path)) {
      console.log(chalk.red(`\n✖  Saved CV not found at: ${config.cv.path}`));
      console.log(chalk.yellow('   Run  npm run setup  to update the path.\n'));
      process.exit(1);
    }
    console.log(chalk.green(`\n✔  Using saved CV: ${path.basename(config.cv.path)}`));
    return parseFile(config.cv.path);
  }

  // Fallback: manual path entry if no config saved yet
  console.log(chalk.yellow('\nNo CV configured. Run  npm run setup  to set one up, or enter a path now.'));

  const { filePath } = await inquirer.prompt<{ filePath: string }>([
    {
      type: 'input',
      name: 'filePath',
      message: 'Path to your CV file (.pdf or .docx):',
      validate: (v) => {
        const p = v.trim();
        if (!p) return 'Required';
        if (!fs.existsSync(p)) return `File not found: ${p}`;
        const ext = path.extname(p).toLowerCase();
        if (ext !== '.pdf' && ext !== '.docx') return 'Only .pdf and .docx files are supported';
        return true;
      },
    },
  ]);

  return parseFile(path.resolve(filePath.trim()));
}

export async function parseFile(filePath: string): Promise<string> {
  const ext = path.extname(filePath).toLowerCase();
  const buffer = fs.readFileSync(filePath);

  if (ext === '.pdf') {
    const data = await pdfParse(buffer);
    return data.text.trim();
  }

  const result = await mammoth.extractRawText({ path: filePath });
  return result.value.trim();
}

import readline from 'readline';
import { chromium, BrowserContext } from 'playwright';
import path from 'path';
import os from 'os';
import fs from 'fs';
import chalk from 'chalk';

const PROFILE_DIR = path.join(os.homedir(), '.job-hunter', 'browser-profile');

export async function createBrowserContext(): Promise<BrowserContext> {
  fs.mkdirSync(PROFILE_DIR, { recursive: true });

  console.log(chalk.gray(`\nUsing browser profile: ${PROFILE_DIR}`));
  console.log(chalk.yellow('On first run, log in to LinkedIn / Indeed in the browser, then return here.\n'));

  const context = await chromium.launchPersistentContext(PROFILE_DIR, {
    headless: process.env.BROWSER_HEADLESS === 'true',
    viewport: { width: 1280, height: 900 },
    args: [
      '--disable-blink-features=AutomationControlled',
      '--no-sandbox',
      '--start-maximized',
    ],
    ignoreHTTPSErrors: true,
  });

  // Mask automation signals
  await context.addInitScript(() => {
    Object.defineProperty(navigator, 'webdriver', { get: () => false });
  });

  return context;
}

export function waitForUser(message: string): Promise<void> {
  return new Promise((resolve) => {
    const rl = readline.createInterface({ input: process.stdin, output: process.stdout });
    rl.question(message, () => {
      rl.close();
      resolve();
    });
  });
}

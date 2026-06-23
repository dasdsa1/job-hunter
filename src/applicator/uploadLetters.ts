import { Page } from 'playwright';
import chalk from 'chalk';
import { FileEntry } from '../fileConfig.js';

const LETTER_LABEL_KEYWORDS = ['recommend', 'letter', 'additional', 'supporting', 'reference'];

export async function tryUploadLetters(page: Page, letters: FileEntry[]): Promise<void> {
  if (!letters.length) return;

  const fileInputs = await page.$$('input[type="file"]');

  for (const input of fileInputs) {
    const labelText = await input.evaluate((el) => {
      const id = el.getAttribute('id');
      const label = id ? document.querySelector(`label[for="${id}"]`) : null;
      return (
        label?.textContent ??
        el.getAttribute('aria-label') ??
        el.getAttribute('name') ??
        ''
      ).toLowerCase();
    });

    const isLetterField = LETTER_LABEL_KEYWORDS.some((kw) => labelText.includes(kw));
    if (!isLetterField) continue;

    const paths = letters.map((l) => l.path).filter(Boolean);
    if (!paths.length) continue;

    try {
      await input.setInputFiles(paths);
      console.log(chalk.green(`  ✔  Uploaded ${paths.length} recommendation letter(s)`));
    } catch {
      console.log(chalk.yellow('  ⚠  Could not auto-upload letters — attach them manually.'));
    }
    return;
  }
}

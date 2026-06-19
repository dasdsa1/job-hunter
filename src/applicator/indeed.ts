import { BrowserContext, Page } from 'playwright';
import chalk from 'chalk';
import ora from 'ora';
import { JobMatch } from '../types.js';
import { waitForUser } from '../scrapers/browser.js';

export async function applyIndeed(
  context: BrowserContext,
  jobMatch: JobMatch,
): Promise<boolean> {
  const { job, coverLetter } = jobMatch;

  if (!job.isEasyApply) {
    console.log(chalk.yellow(`\n⚠  ${job.title} @ ${job.company} — no Indeed Apply button, opening for manual application.`));
    const page = await context.newPage();
    await page.goto(job.url);
    await waitForUser(chalk.cyan('\nPress Enter when you have finished applying manually… '));
    await page.close();
    return true;
  }

  const spinner = ora(`Opening Indeed Apply for ${job.title} @ ${job.company}…`).start();
  const page = await context.newPage();

  try {
    await page.goto(job.url, { waitUntil: 'domcontentloaded', timeout: 30_000 });

    // Click the Indeed Apply button
    const applyBtn = await page.waitForSelector(
      '[data-testid="indeedApplyButton"], .indeed-apply-button, button:has-text("Apply now")',
      { timeout: 10_000 },
    );
    await applyBtn.click();
    spinner.succeed('Indeed Apply form opened');

    await page.waitForTimeout(2_000);

    await fillIndeedForm(page, coverLetter ?? '');

    console.log(chalk.green('\n✔  Form pre-filled. Please review the application in the browser.'));
    console.log(chalk.bold.yellow('⚠  Do NOT submit yet — review everything first.\n'));

    await waitForUser(
      chalk.cyan(
        'Review the form, make any corrections, then press Enter here when ready to submit… ',
      ),
    );

    const submitted = await clickIndeedSubmit(page);
    if (submitted) {
      console.log(chalk.green('\n✔  Application submitted!'));
    } else {
      console.log(chalk.yellow('\n⚠  Submit button not located — click it manually in the browser.'));
      await waitForUser(chalk.cyan('Press Enter once submitted… '));
    }

    return true;
  } catch (err) {
    spinner.fail(`Indeed Apply failed: ${(err as Error).message}`);
    console.log(chalk.yellow(`Opening job page for manual application: ${job.url}`));
    await page.goto(job.url);
    await waitForUser(chalk.cyan('\nPress Enter when finished… '));
    return false;
  } finally {
    await page.close();
  }
}

async function fillIndeedForm(page: Page, coverLetter: string): Promise<void> {
  const maxSteps = 8;

  for (let step = 0; step < maxSteps; step++) {
    // Fill text inputs that are empty
    const inputs = await page.$$('input[type="text"], input[type="tel"], input[type="email"]');
    for (const input of inputs) {
      const val = await input.inputValue().catch(() => '');
      if (!val.trim()) await input.focus().catch(() => null);
    }

    // Insert cover letter into cover letter textarea
    if (coverLetter) {
      const textareas = await page.$$('textarea');
      for (const ta of textareas) {
        const name = await ta.getAttribute('name').catch(() => '');
        const placeholder = await ta.getAttribute('placeholder').catch(() => '');
        const combined = `${name} ${placeholder}`.toLowerCase();
        if (combined.includes('cover') || combined.includes('letter') || combined.includes('message')) {
          const existing = await ta.inputValue().catch(() => '');
          if (!existing.trim()) await ta.fill(coverLetter);
        }
      }
    }

    // Handle required selects — choose first non-placeholder option if blank
    const selects = await page.$$('select');
    for (const sel of selects) {
      const val = await sel.inputValue().catch(() => '');
      if (!val) {
        const options = await sel.$$('option');
        if (options.length > 1) {
          await sel.selectOption({ index: 1 }).catch(() => null);
        }
      }
    }

    // Try to advance to the next step
    const nextBtn = await page.$(
      'button[data-testid="ia-continueButton"], button:has-text("Continue"), button:has-text("Next")',
    );
    if (!nextBtn) break;

    const btnText = await nextBtn.textContent();
    if ((btnText ?? '').toLowerCase().includes('submit')) break;

    await nextBtn.click();
    await page.waitForTimeout(1_200);
  }
}

async function clickIndeedSubmit(page: Page): Promise<boolean> {
  const btn = await page.$(
    'button[data-testid="ia-continueButton"]:has-text("Submit"), button:has-text("Submit my application"), button:has-text("Submit application")',
  );
  if (!btn) return false;
  await btn.click();
  await page.waitForTimeout(2_000);
  return true;
}

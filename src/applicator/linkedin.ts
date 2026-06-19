import { BrowserContext, Page } from 'playwright';
import chalk from 'chalk';
import ora from 'ora';
import { JobMatch } from '../types.js';
import { waitForUser } from '../scrapers/browser.js';

export async function applyLinkedIn(
  context: BrowserContext,
  jobMatch: JobMatch,
): Promise<boolean> {
  const { job, coverLetter } = jobMatch;

  if (!job.isEasyApply) {
    console.log(chalk.yellow(`\n⚠  ${job.title} @ ${job.company} is not Easy Apply — opening in browser for manual application.`));
    const page = await context.newPage();
    await page.goto(job.url);
    await waitForUser(chalk.cyan('\nPress Enter when you have finished applying manually… '));
    await page.close();
    return true;
  }

  const spinner = ora(`Opening Easy Apply for ${job.title} @ ${job.company}…`).start();
  const page = await context.newPage();

  try {
    await page.goto(job.url, { waitUntil: 'domcontentloaded', timeout: 30_000 });

    // Click the Easy Apply button
    const applyBtn = await page.waitForSelector(
      'button.jobs-apply-button:has-text("Easy Apply"), button[aria-label*="Easy Apply"]',
      { timeout: 10_000 },
    );
    await applyBtn.click();
    spinner.succeed('Easy Apply modal opened');

    // Give the modal time to appear
    await page.waitForTimeout(1_500);

    await fillModalSteps(page, coverLetter ?? '');

    console.log(chalk.green('\n✔  Form pre-filled. Please review each step in the browser.'));
    console.log(chalk.bold.yellow('⚠  Do NOT click Submit yet — review everything first.\n'));

    await waitForUser(
      chalk.cyan(
        'Review the application in the browser, make any corrections, then press Enter here when ready to confirm submission… ',
      ),
    );

    // Find and click Submit on the final step
    const submitted = await clickSubmitIfVisible(page);
    if (submitted) {
      console.log(chalk.green('\n✔  Application submitted!'));
    } else {
      console.log(chalk.yellow('\n⚠  Submit button not found — you may need to click it manually.'));
      await waitForUser(chalk.cyan('Press Enter once you have submitted manually… '));
    }

    return true;
  } catch (err) {
    spinner.fail(`Easy Apply failed: ${(err as Error).message}`);
    console.log(chalk.yellow(`Opening job URL for manual application: ${job.url}`));
    await page.goto(job.url);
    await waitForUser(chalk.cyan('\nPress Enter when finished… '));
    return false;
  } finally {
    await page.close();
  }
}

// Iterates through modal steps, filling common fields
async function fillModalSteps(page: Page, coverLetter: string): Promise<void> {
  const maxSteps = 10;

  for (let step = 0; step < maxSteps; step++) {
    await fillCurrentStep(page, coverLetter);

    // Check for a "Next" button (not "Submit")
    const nextBtn = await page.$(
      'button[aria-label="Continue to next step"], button:has-text("Next"), footer button:last-child',
    );
    if (!nextBtn) break;

    const nextText = await nextBtn.textContent();
    if ((nextText ?? '').toLowerCase().includes('submit')) break; // Stop before submit

    await nextBtn.click();
    await page.waitForTimeout(1_000);
  }
}

async function fillCurrentStep(page: Page, coverLetter: string): Promise<void> {
  // Fill empty text inputs (phone, name, etc.)
  const textInputs = await page.$$('input[type="text"]:not([aria-hidden="true"]), input[type="tel"]');
  for (const input of textInputs) {
    const val = await input.inputValue().catch(() => '');
    if (!val.trim()) {
      // Leave blank — the user will fill these; we focus them so they're visible
      await input.focus().catch(() => null);
    }
  }

  // Insert cover letter into any visible cover-letter textarea
  if (coverLetter) {
    const textareas = await page.$$('textarea');
    for (const ta of textareas) {
      const label = await ta.evaluate((el) => {
        const id = el.getAttribute('id');
        const lbl = id ? document.querySelector(`label[for="${id}"]`) : null;
        return (lbl?.textContent ?? el.getAttribute('aria-label') ?? el.getAttribute('placeholder') ?? '').toLowerCase();
      });
      if (label.includes('cover') || label.includes('letter') || label.includes('additional')) {
        const existing = await ta.inputValue().catch(() => '');
        if (!existing.trim()) {
          await ta.fill(coverLetter);
        }
      }
    }
  }

  // Handle required radio groups — select first option if none selected
  const radioGroups = await page.$$('fieldset');
  for (const group of radioGroups) {
    const checked = await group.$('input[type="radio"]:checked');
    if (!checked) {
      const firstRadio = await group.$('input[type="radio"]');
      if (firstRadio) await firstRadio.check().catch(() => null);
    }
  }
}

async function clickSubmitIfVisible(page: Page): Promise<boolean> {
  const submitBtn = await page.$(
    'button[aria-label="Submit application"], button:has-text("Submit application")',
  );
  if (!submitBtn) return false;
  await submitBtn.click();
  await page.waitForTimeout(2_000);
  return true;
}

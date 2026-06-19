import { BrowserContext, Page } from 'playwright';
import chalk from 'chalk';
import ora from 'ora';
import { JobListing, SearchConfig } from '../types.js';
import { waitForUser } from './browser.js';

export async function scrapeLinkedIn(
  context: BrowserContext,
  config: SearchConfig,
): Promise<JobListing[]> {
  const spinner = ora('Scraping LinkedIn jobs…').start();
  const page = await context.newPage();
  const jobs: JobListing[] = [];

  try {
    const searchUrl = buildSearchUrl(config);
    await page.goto(searchUrl, { waitUntil: 'domcontentloaded', timeout: 30_000 });

    // Redirect to login page means the user isn't logged in
    if (page.url().includes('/login') || page.url().includes('/authwall')) {
      spinner.warn('Not logged in to LinkedIn');
      await waitForUser(
        chalk.yellow('\nLog in to LinkedIn in the browser window, then press Enter here… '),
      );
      await page.goto(searchUrl, { waitUntil: 'domcontentloaded', timeout: 30_000 });
    }

    // Wait for the job list container
    await page
      .waitForSelector('.jobs-search-results-list, .jobs-search__results-list', { timeout: 15_000 })
      .catch(() => null);

    // Trigger lazy-load by scrolling
    for (let i = 0; i < 3; i++) {
      await page.evaluate(() => window.scrollBy(0, 600));
      await page.waitForTimeout(700);
    }

    const cardSelector = 'li.jobs-search-results__list-item, li[data-occludable-job-id]';
    const cards = await page.$$(cardSelector);

    for (let i = 0; i < Math.min(cards.length, config.maxJobsPerSite); i++) {
      try {
        await cards[i].click();
        await page.waitForTimeout(1_200);

        const job = await extractJob(page, `li-${i}`);
        if (!job) continue;
        if (config.easyApplyOnly && !job.isEasyApply) continue;

        jobs.push(job);
        spinner.text = `LinkedIn: scraped ${jobs.length} job(s)…`;
      } catch {
        // Skip unparseable cards
      }
    }

    spinner.succeed(`LinkedIn: found ${jobs.length} job(s)`);
  } catch (err) {
    spinner.fail(`LinkedIn scraping failed: ${(err as Error).message}`);
  } finally {
    await page.close();
  }

  return jobs;
}

function buildSearchUrl(config: SearchConfig): string {
  const keywords = [config.jobTitle, config.keywords].filter(Boolean).join(' ');
  const params = new URLSearchParams({ keywords, location: config.location });
  if (config.easyApplyOnly) params.set('f_LF', 'f_AL');
  return `https://www.linkedin.com/jobs/search/?${params.toString()}`;
}

async function extractJob(page: Page, idx: string): Promise<JobListing | null> {
  const title = await safeText(page, [
    '.jobs-unified-top-card__job-title',
    '.job-details-jobs-unified-top-card__job-title',
    'h1.t-24',
  ]);
  const company = await safeText(page, [
    '.jobs-unified-top-card__company-name a',
    '.jobs-unified-top-card__company-name',
    '.job-details-jobs-unified-top-card__company-name',
  ]);
  const location = await safeText(page, [
    '.jobs-unified-top-card__bullet',
    '.job-details-jobs-unified-top-card__bullet',
    '.jobs-unified-top-card__workplace-type',
  ]);
  const description = await safeText(page, [
    '.jobs-description-content__text',
    '.jobs-description__content',
    '#job-details',
  ]);

  if (!title || !company) return null;

  const applyBtn = await page.$('button.jobs-apply-button');
  const applyText = applyBtn ? await applyBtn.textContent() : '';
  const isEasyApply = (applyText ?? '').toLowerCase().includes('easy apply');

  const url = page.url();
  const jobIdMatch = url.match(/\/jobs\/view\/(\d+)/);

  return {
    id: `linkedin-${jobIdMatch?.[1] ?? idx}`,
    title: title.trim(),
    company: company.trim(),
    location: location.trim(),
    description: description.slice(0, 3_500).trim(),
    url,
    source: 'linkedin',
    isEasyApply,
  };
}

async function safeText(page: Page, selectors: string[]): Promise<string> {
  for (const sel of selectors) {
    try {
      const el = await page.$(sel);
      if (el) {
        const text = await el.textContent();
        if (text?.trim()) return text.trim();
      }
    } catch {
      // try next selector
    }
  }
  return '';
}

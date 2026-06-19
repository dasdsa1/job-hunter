import { BrowserContext, Page } from 'playwright';
import chalk from 'chalk';
import ora from 'ora';
import { JobListing, SearchConfig } from '../types.js';
import { waitForUser } from './browser.js';

export async function scrapeIndeed(
  context: BrowserContext,
  config: SearchConfig,
): Promise<JobListing[]> {
  const spinner = ora('Scraping Indeed jobs…').start();
  const page = await context.newPage();
  const jobs: JobListing[] = [];

  try {
    const searchUrl = buildSearchUrl(config);
    await page.goto(searchUrl, { waitUntil: 'domcontentloaded', timeout: 30_000 });

    if (page.url().includes('/account/login') || page.url().includes('/promo/resume')) {
      spinner.warn('Possible Indeed login wall detected');
      await waitForUser(
        chalk.yellow('\nIf prompted, log in to Indeed in the browser, then press Enter here… '),
      );
    }

    // Wait for at least one job card
    await page
      .waitForSelector('[data-jk], .jobsearch-ResultsList li', { timeout: 15_000 })
      .catch(() => null);

    // Collect all job cards on the page
    const cardSelector = '[data-jk], .jobsearch-ResultsList li.css-1ac2h1w, .result';
    const cards = await page.$$(cardSelector);

    for (let i = 0; i < Math.min(cards.length, config.maxJobsPerSite); i++) {
      try {
        // Scroll card into view and click
        await cards[i].scrollIntoViewIfNeeded();
        await cards[i].click();
        await page.waitForTimeout(1_200);

        const job = await extractJob(page, i);
        if (job) {
          if (config.easyApplyOnly && !job.isEasyApply) continue;
          jobs.push(job);
          spinner.text = `Indeed: scraped ${jobs.length} job(s)…`;
        }
      } catch {
        // Skip
      }
    }

    spinner.succeed(`Indeed: found ${jobs.length} job(s)`);
  } catch (err) {
    spinner.fail(`Indeed scraping failed: ${(err as Error).message}`);
  } finally {
    await page.close();
  }

  return jobs;
}

function buildSearchUrl(config: SearchConfig): string {
  const q = [config.jobTitle, config.keywords].filter(Boolean).join(' ');
  const params = new URLSearchParams({ q, l: config.location });
  return `https://www.indeed.com/jobs?${params.toString()}`;
}

async function extractJob(page: Page, idx: number): Promise<JobListing | null> {
  // After clicking a card Indeed opens a right-side detail panel
  const title = await safeText(page, [
    '.jobsearch-JobInfoHeader-title',
    'h1.jobsearch-JobInfoHeader-title',
    '[data-testid="simpleTitle"]',
    'h2.jobTitle span',
  ]);
  const company = await safeText(page, [
    '[data-testid="inlineHeader-companyName"] a',
    '[data-testid="inlineHeader-companyName"]',
    '.jobsearch-CompanyInfoWithoutHeaderImage a',
    'span.companyName',
  ]);
  const location = await safeText(page, [
    '[data-testid="job-location"]',
    '.jobsearch-JobInfoHeader-subtitle .jobsearch-JobInfoHeader-locationName',
    'div.companyLocation',
  ]);
  const description = await safeText(page, [
    '#jobDescriptionText',
    '.jobsearch-jobDescriptionText',
    '[data-testid="jobsearch-JobComponent-description"]',
  ]);

  if (!title || !company) return null;

  // Detect Indeed Apply (quick apply) vs apply on company site
  const applyBtn = await page.$('[data-testid="indeedApplyButton"], .indeed-apply-button');
  const isEasyApply = applyBtn !== null;

  const url = page.url();
  const jkMatch = url.match(/[?&]jk=([a-zA-Z0-9]+)/);

  return {
    id: `indeed-${jkMatch?.[1] ?? idx}`,
    title: title.trim(),
    company: company.trim(),
    location: location.trim(),
    description: description.slice(0, 3_500).trim(),
    url,
    source: 'indeed',
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
      // try next
    }
  }
  return '';
}

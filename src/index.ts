import 'dotenv/config';
import chalk from 'chalk';
import inquirer from 'inquirer';
import { collectResume } from './resume.js';
import { createBrowserContext } from './scrapers/browser.js';
import { scrapeLinkedIn } from './scrapers/linkedin.js';
import { scrapeIndeed } from './scrapers/indeed.js';
import { matchJobs } from './matcher.js';
import { generateCoverLetter } from './coverLetter.js';
import { applyLinkedIn } from './applicator/linkedin.js';
import { applyIndeed } from './applicator/indeed.js';
import { generateReport } from './reporter.js';
import { SearchConfig, JobMatch, RunReport } from './types.js';

async function main() {
  printBanner();
  validateEnv();

  // ── 1. Resume ──────────────────────────────────────────────────────────────
  const resume = await collectResume();
  if (!resume) {
    console.log(chalk.red('No resume provided. Exiting.'));
    process.exit(1);
  }
  console.log(chalk.green(`\n✔  Resume captured (${resume.length} chars)\n`));

  // ── 2. Search config ───────────────────────────────────────────────────────
  const config = await promptSearchConfig();

  // ── 3. Scrape ──────────────────────────────────────────────────────────────
  console.log(chalk.bold('\n━━━  SCRAPING JOBS  ━━━'));
  const context = await createBrowserContext();

  let allJobs = [];
  try {
    if (config.sites.includes('linkedin')) {
      const lJobs = await scrapeLinkedIn(context, config);
      allJobs.push(...lJobs);
    }
    if (config.sites.includes('indeed')) {
      const iJobs = await scrapeIndeed(context, config);
      allJobs.push(...iJobs);
    }
  } catch (err) {
    console.error(chalk.red(`Scraping error: ${(err as Error).message}`));
  }

  if (allJobs.length === 0) {
    console.log(chalk.yellow('\nNo jobs found. Try broader search terms or different sites.'));
    await context.close();
    return;
  }

  console.log(chalk.green(`\n✔  Total jobs scraped: ${allJobs.length}`));

  // ── 4. Match ───────────────────────────────────────────────────────────────
  console.log(chalk.bold('\n━━━  MATCHING JOBS  ━━━'));
  const scoreMap = await matchJobs(allJobs, resume);

  const jobMatches: JobMatch[] = allJobs
    .map((job) => ({
      job,
      match: scoreMap.get(job.id) ?? { score: 0, summary: 'No score available', reasons: [] },
      applied: false,
    }))
    .sort((a, b) => b.match.score - a.match.score);

  const qualified = jobMatches.filter((m) => m.match.score >= config.minScore);

  if (qualified.length === 0) {
    console.log(chalk.yellow(`\nNo jobs scored ≥ ${config.minScore}. Lowering your min score or broadening your search may help.`));
    await context.close();
    generateReport(buildReport(config, allJobs.length, 0, jobMatches));
    return;
  }

  // ── 5. Show results and let user pick ──────────────────────────────────────
  console.log(chalk.bold('\n━━━  MATCHING RESULTS  ━━━\n'));
  printMatchTable(qualified);

  const { selectedIds } = await inquirer.prompt<{ selectedIds: string[] }>([
    {
      type: 'checkbox',
      name: 'selectedIds',
      message: 'Select jobs to apply to (Space to select, Enter to confirm):',
      choices: qualified.map((m) => ({
        name: `[${m.match.score}/10] ${m.job.title} @ ${m.job.company} (${m.job.source})`,
        value: m.job.id,
        checked: m.match.score >= 8,
      })),
    },
  ]);

  if (selectedIds.length === 0) {
    console.log(chalk.yellow('\nNo jobs selected. Generating report…'));
    const htmlPath = generateReport(buildReport(config, allJobs.length, qualified.length, jobMatches));
    console.log(chalk.green(`\n✔  Report saved to ${htmlPath}`));
    await context.close();
    return;
  }

  // ── 6. Apply ───────────────────────────────────────────────────────────────
  console.log(chalk.bold('\n━━━  APPLYING  ━━━'));

  for (const jm of jobMatches) {
    if (!selectedIds.includes(jm.job.id)) continue;

    console.log(chalk.cyan(`\n▶  ${jm.job.title} @ ${jm.job.company}`));
    console.log(chalk.gray(`   Score: ${jm.match.score}/10 — ${jm.match.summary}`));

    // Generate cover letter
    console.log(chalk.bold('\n── Cover Letter ──'));
    jm.coverLetter = await generateCoverLetter(jm.job, jm.match, resume);

    // Confirm before proceeding
    const { proceed } = await inquirer.prompt<{ proceed: boolean }>([
      {
        type: 'confirm',
        name: 'proceed',
        message: 'Proceed to apply with this cover letter?',
        default: true,
      },
    ]);

    if (!proceed) {
      console.log(chalk.gray('Skipped.'));
      continue;
    }

    // Launch the apply flow
    let success = false;
    if (jm.job.source === 'linkedin') {
      success = await applyLinkedIn(context, jm);
    } else {
      success = await applyIndeed(context, jm);
    }

    jm.applied = success;
    jm.applicationStatus = success ? 'submitted' : 'failed';
  }

  // ── 7. Report ──────────────────────────────────────────────────────────────
  console.log(chalk.bold('\n━━━  REPORT  ━━━'));
  const htmlPath = generateReport(buildReport(config, allJobs.length, qualified.length, jobMatches));

  await context.close();

  const appliedCount = jobMatches.filter((m) => m.applied).length;
  console.log(chalk.bold.green(`\n✔  Done!`));
  console.log(`   Jobs scraped:  ${allJobs.length}`);
  console.log(`   Jobs matched:  ${qualified.length}`);
  console.log(`   Applied:       ${appliedCount}`);
  console.log(`   Report saved:  ${htmlPath}\n`);
}

// ── Helpers ────────────────────────────────────────────────────────────────

function printBanner() {
  console.log(chalk.bold.blue(`
  ╔═══════════════════════════════╗
  ║     JOB HUNTER  (Claude AI)   ║
  ╚═══════════════════════════════╝`));
  console.log(chalk.gray('  Automated job search & application assistant\n'));
}

function validateEnv() {
  if (!process.env.GOOGLE_API_KEY) {
    console.error(chalk.red('❌  GOOGLE_API_KEY is not set. Copy .env.example to .env and fill it in.'));
    process.exit(1);
  }
}

async function promptSearchConfig(): Promise<SearchConfig> {
  console.log(chalk.bold('\n━━━  SEARCH CONFIG  ━━━\n'));

  const answers = await inquirer.prompt<{
    jobTitle: string;
    location: string;
    keywords: string;
    sites: Array<'linkedin' | 'indeed'>;
    minScore: number;
    maxJobsPerSite: number;
    easyApplyOnly: boolean;
  }>([
    {
      type: 'input',
      name: 'jobTitle',
      message: 'Job title to search for:',
      validate: (v) => v.trim().length > 0 || 'Required',
    },
    {
      type: 'input',
      name: 'location',
      message: 'Location (city, "Remote", etc.):',
      default: 'Remote',
    },
    {
      type: 'input',
      name: 'keywords',
      message: 'Additional keywords (optional):',
      default: '',
    },
    {
      type: 'checkbox',
      name: 'sites',
      message: 'Which sites to search?',
      choices: [
        { name: 'LinkedIn', value: 'linkedin', checked: true },
        { name: 'Indeed', value: 'indeed', checked: true },
      ],
      validate: (v) => v.length > 0 || 'Select at least one site',
    },
    {
      type: 'number',
      name: 'minScore',
      message: 'Minimum match score to consider (1–10):',
      default: parseInt(process.env.MIN_MATCH_SCORE ?? '6', 10),
      validate: (v) => (v >= 1 && v <= 10) || 'Must be 1–10',
    },
    {
      type: 'number',
      name: 'maxJobsPerSite',
      message: 'Max jobs to scrape per site:',
      default: parseInt(process.env.MAX_JOBS_PER_SITE ?? '20', 10),
      validate: (v) => v > 0 || 'Must be > 0',
    },
    {
      type: 'confirm',
      name: 'easyApplyOnly',
      message: 'Only show Easy Apply / Indeed Apply jobs?',
      default: true,
    },
  ]);

  return {
    jobTitle: answers.jobTitle.trim(),
    location: answers.location.trim(),
    keywords: answers.keywords.trim(),
    sites: answers.sites,
    minScore: answers.minScore,
    maxJobsPerSite: answers.maxJobsPerSite,
    easyApplyOnly: answers.easyApplyOnly,
  };
}

function printMatchTable(matches: JobMatch[]) {
  const scoreTag = (n: number) =>
    n >= 8
      ? chalk.green(`${n}/10`)
      : n >= 6
        ? chalk.yellow(`${n}/10`)
        : chalk.red(`${n}/10`);

  for (const m of matches) {
    console.log(
      `  ${scoreTag(m.match.score)}  ${chalk.bold(m.job.title)} @ ${m.job.company}  ${chalk.gray(m.job.location)}  [${m.job.source}]`,
    );
    console.log(chalk.gray(`         ${m.match.summary}`));
    if (m.match.reasons.length) {
      console.log(chalk.gray(`         • ${m.match.reasons.slice(0, 3).join('  • ')}`));
    }
    console.log();
  }
}

function buildReport(
  config: SearchConfig,
  totalScraped: number,
  totalMatched: number,
  jobMatches: JobMatch[],
): RunReport {
  return {
    timestamp: new Date().toISOString(),
    searchConfig: config,
    totalScraped,
    totalMatched,
    jobMatches,
  };
}

main().catch((err) => {
  console.error(chalk.red(`\nFatal error: ${err.message}`));
  process.exit(1);
});

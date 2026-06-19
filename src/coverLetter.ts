import { GoogleGenerativeAI } from '@google/generative-ai';
import chalk from 'chalk';
import ora from 'ora';
import { JobListing, MatchResult } from './types.js';
import { rateLimiter } from './rateLimiter.js';

const genAI = new GoogleGenerativeAI(process.env.GOOGLE_API_KEY!);
const MODEL = process.env.GEMINI_MODEL ?? 'gemini-2.0-flash-lite';

export async function generateCoverLetter(
  job: JobListing,
  match: MatchResult,
  resume: string,
): Promise<string> {
  const spinner = ora(`Generating cover letter for ${job.title} @ ${job.company}…`).start();

  const prompt = `Write a compelling, concise cover letter for the following job application.

Job Title: ${job.title}
Company: ${job.company}
Location: ${job.location}
Why I'm a good fit (AI analysis): ${match.summary}
Key matching reasons: ${match.reasons.join('; ')}

Job Description (excerpt):
${job.description.slice(0, 1_500)}

My Resume:
---
${resume}
---

Instructions:
- 3 short paragraphs, under 280 words total
- First paragraph: genuine excitement for THIS specific role/company (not generic)
- Second paragraph: 2-3 concrete skills or experiences from my resume that directly match the job requirements
- Third paragraph: brief closing with availability and enthusiasm
- Use first person, active voice
- Do NOT include placeholders like [Your Name] — write it as a finished letter ready to send
- Do NOT add a subject line, date, or address block — just the body paragraphs`;

  try {
    await rateLimiter.throttle();

    const model = genAI.getGenerativeModel({ model: MODEL });
    const stream = await model.generateContentStream(prompt);

    spinner.stop();
    process.stdout.write('\n');

    let letter = '';
    for await (const chunk of stream.stream) {
      const text = chunk.text();
      process.stdout.write(text);
      letter += text;
    }

    process.stdout.write('\n');
    console.log(chalk.green(`✔  Cover letter ready (${letter.trim().split(/\s+/).length} words)`));
    return letter.trim();
  } catch (err) {
    spinner.fail(`Cover letter generation failed: ${(err as Error).message}`);
    return '';
  }
}

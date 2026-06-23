import fs from 'fs';
import path from 'path';
import { GoogleGenerativeAI } from '@google/generative-ai';
import chalk from 'chalk';
import ora from 'ora';
import { Document, Paragraph, TextRun, HeadingLevel, AlignmentType, Packer } from 'docx';
import { JobListing, MatchResult } from './types.js';
import { rateLimiter } from './rateLimiter.js';

const genAI = new GoogleGenerativeAI(process.env.GOOGLE_API_KEY!);
const MODEL = process.env.GEMINI_MODEL ?? 'gemini-2.0-flash-lite';

export async function tailorCv(
  originalCvText: string,
  job: JobListing,
  match: MatchResult,
): Promise<string> {
  const spinner = ora(`Tailoring CV for ${job.title} @ ${job.company}…`).start();

  const prompt = `You are a professional CV writer. Rewrite the candidate's CV to better match the job listing below.

Rules:
- Keep all information factually accurate — do NOT invent experience or skills
- Reorder bullet points and sections to highlight the most relevant experience first
- Rephrase descriptions to mirror the language used in the job posting (keywords, phrasing)
- Remove or de-emphasise experience unrelated to this role
- Keep the same general structure (sections like Experience, Education, Skills)
- Output ONLY the CV text, no commentary, no markdown code fences

Job Title: ${job.title}
Company: ${job.company}
Why candidate matches: ${match.summary}
Key matching points: ${match.reasons.join('; ')}

Job Description:
${job.description.slice(0, 2_000)}

Original CV:
---
${originalCvText}
---`;

  try {
    await rateLimiter.throttle();

    const model = genAI.getGenerativeModel({ model: MODEL });
    const result = await model.generateContent(prompt);
    const tailored = result.response.text().trim();

    spinner.succeed(`Tailored CV ready (${tailored.split(/\s+/).length} words)`);
    return tailored;
  } catch (err) {
    spinner.fail(`CV tailoring failed: ${(err as Error).message}`);
    return originalCvText;
  }
}

export async function saveTailoredCvAsDocx(
  cvText: string,
  job: JobListing,
): Promise<string> {
  const outDir = 'data';
  fs.mkdirSync(outDir, { recursive: true });

  const safeName = `${job.company}-${job.title}`.replace(/[^a-zA-Z0-9-_ ]/g, '').slice(0, 60);
  const outPath = path.join(outDir, `CV-${safeName}.docx`);

  const paragraphs = cvText.split('\n').map((line) => {
    const trimmed = line.trim();

    // Detect section headings (all-caps lines or lines ending with colon)
    const isHeading =
      (trimmed === trimmed.toUpperCase() && trimmed.length > 2 && !/^\d/.test(trimmed)) ||
      /^[A-Z][A-Za-z\s]+:$/.test(trimmed);

    if (!trimmed) return new Paragraph({ text: '' });

    if (isHeading) {
      return new Paragraph({
        text: trimmed,
        heading: HeadingLevel.HEADING_2,
        spacing: { before: 240, after: 60 },
      });
    }

    const isBullet = trimmed.startsWith('•') || trimmed.startsWith('-') || trimmed.startsWith('*');
    return new Paragraph({
      alignment: AlignmentType.LEFT,
      spacing: { after: 60 },
      bullet: isBullet ? { level: 0 } : undefined,
      children: [
        new TextRun({
          text: isBullet ? trimmed.replace(/^[•\-*]\s*/, '') : trimmed,
          size: 22,
          font: 'Calibri',
        }),
      ],
    });
  });

  const doc = new Document({
    sections: [{ children: paragraphs }],
  });

  const buffer = await Packer.toBuffer(doc);
  fs.writeFileSync(outPath, buffer);

  console.log(chalk.green(`✔  Tailored CV saved to ${outPath}`));
  return outPath;
}

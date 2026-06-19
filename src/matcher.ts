import { GoogleGenerativeAI } from '@google/generative-ai';
import ora from 'ora';
import { JobListing, MatchResult } from './types.js';
import { rateLimiter } from './rateLimiter.js';

const genAI = new GoogleGenerativeAI(process.env.GOOGLE_API_KEY!);
const MODEL = process.env.GEMINI_MODEL ?? 'gemini-2.0-flash-lite';

interface RawMatch {
  id: string;
  score: number;
  summary: string;
  reasons: string[];
}

export async function matchJobs(
  jobs: JobListing[],
  resume: string,
): Promise<Map<string, MatchResult>> {
  const spinner = ora(`Scoring ${jobs.length} job(s) against your resume…`).start();

  const jobSnippets = jobs.map((j) => ({
    id: j.id,
    title: j.title,
    company: j.company,
    location: j.location,
    description: j.description.slice(0, 1_000),
  }));

  const prompt = `You are a professional job application assistant.
Evaluate each job listing against the candidate's resume and assign a match score.

Scoring guide:
  9-10  — Near-perfect fit: skills, seniority, and domain all align
  7-8   — Strong match: most requirements met, minor gaps
  5-6   — Moderate: roughly half the requirements match
  3-4   — Weak: significant skill or experience gaps
  1-2   — Poor fit

Resume:
---
${resume}
---

Jobs to evaluate (JSON):
${JSON.stringify(jobSnippets, null, 2)}

Return a JSON array — one object per job:
[
  {
    "id": "<job id>",
    "score": <1-10 integer>,
    "summary": "<one sentence explaining the score>",
    "reasons": ["<reason 1>", "<reason 2>", "<reason 3>"]
  }
]`;

  try {
    await rateLimiter.throttle();

    const model = genAI.getGenerativeModel({
      model: MODEL,
      generationConfig: { responseMimeType: 'application/json' },
    });

    const result = await model.generateContent(prompt);
    const raw = result.response.text();
    const parsed: RawMatch[] = JSON.parse(raw);

    const scoreMap = new Map<string, MatchResult>();
    for (const item of parsed) {
      scoreMap.set(item.id, {
        score: Math.max(1, Math.min(10, Math.round(item.score))),
        summary: item.summary ?? '',
        reasons: Array.isArray(item.reasons) ? item.reasons : [],
      });
    }

    spinner.succeed(`Scored ${scoreMap.size} job(s)`);
    return scoreMap;
  } catch (err) {
    spinner.fail(`Matching failed: ${(err as Error).message}`);
    return new Map();
  }
}

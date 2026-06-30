# LLM Resume/Job Matching

**Sources:** PitchMeAI (2026); HeroHunt (2026); 47billion (2026); Mercity AI (2026); MSLEF arXiv paper (2025); Context-Aware Multi-Agent Resume Screening arXiv paper (2025); MDPI Zero-Shot Resume-Job Matching (2025)
**Raw:** [llm-resume-job-matching-best-practices.md](../../raw/llm-job-matching/2026-07-01-llm-resume-job-matching-best-practices.md)
**Updated:** 2026-07-01

## Summary

Current industry consensus on building LLM-based resume/job matching systems, and where
the Job Hunter project (this repo) currently sits relative to it.

## Categorical scores beat raw numeric scores for consistency

LLMs are more internally consistent picking between discrete categories (A/B/C, or
Strong/Moderate/Weak) than assigning a precise number on a 1-100 scale. Numeric scores
drift batch-to-batch even with identical inputs, because the model has no stable anchor
for "73 vs 76." Grading on a coarse scale (or a small integer range like 1-10) is a
middle ground that keeps enough resolution to rank candidates/jobs while staying stable.

**Applied:** `GeminiService.MatchJobsAsync` uses a 1-10 integer score, not raw 0-100 —
already aligned with this guidance, though not as coarse as a pure A/B/C grade.

## Decompose job requirements before scoring

Split job description requirements into "non-negotiable" (e.g. "3+ years backend Java")
and "nice-to-have" (e.g. "familiarity with AWS") *before* asking the model to score —
rather than asking it to evaluate the whole listing holistically against the whole resume
in one pass. This reduces the chance the model over-weights an impressive nice-to-have
while missing a hard requirement gap.

**Applied:** Not yet — current prompt scores holistically. This is a concrete, scoped
improvement: a pre-processing step (or part of the same prompt) that asks the model to
first extract must-have vs nice-to-have from the listing, then score against each bucket
separately.

## Structured output / schema enforcement

Free-form JSON parsing (regex or manual tree-walking) is fragile — a single malformed
response can break the whole batch. Modern LLM APIs (including Gemini's `responseSchema`)
support constrained/structured generation that guarantees the response matches a schema,
eliminating an entire class of parse failures.

**Applied:** Not yet — current implementation parses with `JsonNode` inside a single
try/catch around the whole batch (one malformed item loses the batch's results, though
not the whole run). Switching to `responseSchema` is the single highest-leverage
reliability fix available.

## Resume profile extraction vs. re-sending raw text

Re-sending the full resume text in every batch call multiplies token cost linearly with
batch count, and gives the model no more signal each time — it's the same text. A
one-time structured extraction pass (skills list, years of experience, seniority level,
domain) sent once, reused across all batches, is both cheaper and arguably more
consistent (the model scores against a stable structured profile instead of re-reading
unstructured prose every time).

**Applied:** Not yet — current implementation sends full resume text in every batch
(`GeminiService.cs`). Backlogged.

## Semantic similarity as a complement, not a replacement

Embedding-based cosine similarity between resume and job description sections is a
faster, cheaper pre-filter that can run before the expensive LLM-as-judge call — useful
for very large candidate pools, less relevant at personal-job-search scale (a few dozen
listings per run).

**Applied:** Not applicable at current project scale.

## Multi-agent decomposition

Some research frameworks split resume extraction, evaluation, summarization, and score
formatting into four separate agent calls, each with a narrow responsibility. This adds
latency and cost for marginal accuracy gain — appropriate at enterprise ATS scale, not
obviously worth it for scoring a few dozen jobs per run.

**Applied:** Deliberately not adopted — single-call-per-batch is the right tradeoff at
this project's scale. Revisit only if batch-level scoring quality becomes a measured
problem.

## See Also
- [AI Hiring Ethics and Regulation](../ai-ethics/ai-hiring-ethics-regulation.md) — human-in-the-loop requirement that shapes how this matching output is *used*, not just generated

# AI Scoring — Design & Best Practices

## Current approach

`GeminiService.MatchJobsAsync` sends batches of 20 jobs + full resume text per call,
asks for a 1-10 score plus advisory copy framed as an HR consultant talking directly to
the candidate (see commit `8bd1b25`). Output also includes optional `redFlags` (vague
comp, generic listing language, agency reposts).

## What the research says, and where we diverge

**Categorical > numeric scores for consistency.** Industry practice increasingly favors
grades (A/B/C) over raw 1-100 scores, since LLMs are more internally consistent picking a
category than a precise number. We use 1-10, which is a middle ground — fine-grained
enough to rank, coarse enough to stay stable. If batch-to-batch score drift becomes a
problem, collapsing to a 5-band scale (Strong/Good/Moderate/Weak/Poor + numeric tiebreak)
is the next step — tracked in `todo.md`.

**Structured output / schema enforcement.** We currently parse free-form JSON via manual
`JsonNode` walking wrapped in a single try/catch per batch — one malformed response loses
the whole batch's scores. Gemini supports `responseSchema` for guaranteed shape; this is
the highest-leverage accuracy fix on the backlog (see [[Roadmap]]).

**Split non-negotiable vs nice-to-have requirements.** Best practice is parsing the job
listing into "must-have" and "nice-to-have" buckets before scoring, rather than scoring
holistically. We don't do this yet — the prompt scores holistically against the raw
resume text. Worth revisiting if false-positive matches become common.

**Resume re-sent every batch.** We currently re-send the full resume text in every batch
call (3 batches of 20 jobs = 3x the resume tokens). A one-time structured extraction
(skills, years experience, seniority) sent once instead of raw text would cut cost and
plausibly improve consistency — also on the backlog.

**Multi-agent decomposition.** Some frameworks split resume extraction, evaluation, and
formatting into separate agent calls. We deliberately keep this single-call per batch —
the cost/latency tradeoff isn't worth it at our scale (a few dozen jobs per run, not
enterprise ATS volume).

## Human-in-the-loop (non-negotiable)

Every apply action requires explicit user confirmation — `RunViewModel`'s
`ReviewRequest`/`CvChoiceRequest`/`LetterChoiceRequest` flow always pauses for a human
decision before submitting anything. This isn't just UX — it's the actual ethical
baseline industry guidance converges on: AI scores a candidate's *own* job search, but a
human approves every outbound action. (Different risk profile than employer-side
screening tools, which face real bias/discrimination liability — Workday's screening
tool is under an active ADEA class action as of 2026 for exactly this reason.)

## Bias note

This tool scores jobs *for* the candidate, not candidates *for* an employer — the asymmetry
that drives most AI-hiring bias law doesn't directly apply. Still worth being honest that
the model's judgment of "good fit" can encode its own blind spots (e.g. undervaluing
non-traditional backgrounds). No mitigation implemented; flagging as a known limitation,
not a solved problem.

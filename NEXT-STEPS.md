# job-hunter — Next Steps

> Summary companion to [todo.md](todo.md). Last refreshed: 2026-07-01.
> State: **~80% — works for personal use.** .NET 9 Worker + WPF UI + parallel TS/Node CLI.

## Done recently

- **Scheduled headless runs** — `SEARCH_INTERVAL_HOURS` loops the Worker without external cron.
- **Result notifications** — generic webhook POST (Slack/Discord-compatible) on run finish.
- **Resume profile extraction** — parse once into structured skills/experience instead of
  resending raw text per scoring batch.
- **Gemini structured output** — `responseSchema` instead of manual `JsonNode` parsing.
- **Fuzzy cross-source dedup** — Levenshtein on normalized title (catches Adzuna re-posting
  Remotive listings).
- **Multi-provider LLM fallback** + pre-flight "Test connection" for Gemini/Adzuna keys.
- **HR consultant persona** — RedFlags + "Consultant's Take"/"Fit" in UI and HTML report.
- **CI** — Dependabot + gitleaks, Dockerfile build verification, Worker test project.

## Concrete next actionable items

1. **Tests (biggest gap)** — `GeminiService` (JSON parsing, retry/backoff, batch splitting),
   `ResumeParserService` (PDF/DOCX extraction), more `ApiJobSources.Dedup` edge cases,
   extract and test `RunViewModel` orchestration (skip-applied, etc.).
2. **UX** — first-run checklist/wizard; negative filters (exclude companies/keywords);
   progress bar during Gemini scoring batches.
3. **Platform alignment decision (.NET 9 → 8 LTS?)** — platform standard is .NET 8 LTS;
   this repo is on .NET 9. Either down-align or formally isolate it as a module that stays
   on 9, and **record the call in the wiki decision log**. Do not silently mix.

## Platform-integration candidate (do NOT build ahead of an explicit TODO)

- job-hunter has **no structured CV/curriculum model** — a resume is just a file path on
  disk. `platform-schemas` now ships a `Curriculum` (CBIF) entity, so adopting it is the
  natural way to close this gap **when an integration workflow actually requires it**. Per
  this repo's CLAUDE.md, don't invent the CV model ahead of a TODO asking for it.

## Blockers / needs the user

- **API keys** — Gemini / Adzuna (and any additional LLM provider for the fallback chain);
  bring-your-own-key via env vars, never committed.
- **Webhook URL** for run-finished notifications (Slack/Discord), if used.
- The .NET version-alignment decision above needs an owner call before platform integration.

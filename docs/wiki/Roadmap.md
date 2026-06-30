# Roadmap

Mirrors `todo.md` in the repo root — that file is the source of truth, update it first.

## Automation
- Scheduled headless runs — `--watch`/`SEARCH_INTERVAL_HOURS` so Worker loops without external cron
- CI: Worker test project (WorkerConfigService env parsing)
- CI: verify Dockerfile builds (caught 3 local breaks that should've failed in CI)
- CI: dependency/secret scanning (Dependabot, gitleaks)
- Result notifications — webhook/email/Slack ping when a headless run finishes

## Accuracy
- Gemini structured output — `responseSchema` instead of manual JsonNode parsing (see [[AI-Scoring-Best-Practices]])
- Resume profile extraction — parse once into structured skills/experience, stop resending raw text per batch
- Fuzzy cross-source dedup — Levenshtein on normalized title
- Use captured salary data (RemoteOK/Adzuna) in scoring/filtering — currently dead data

## UX
- First-run checklist/wizard
- Negative filters — exclude companies/keywords
- Progress bar during Gemini scoring batches

## Tests
- GeminiService — JSON parsing, retry/backoff, batch splitting
- ResumeParserService — PDF/DOCX extraction
- ApiJobSources.Dedup — more edge cases
- RunViewModel — extractable orchestration logic (skip-applied, etc.)

## Done
- HR consultant persona (prompt + RedFlags + UI labels)
- Pre-flight Test Connection buttons (Gemini, Adzuna)
- Retired-model auto-migration (config.json + Worker)
- Headless API sources (Remotive, RemoteOK, Arbeitnow, Adzuna)
- Docker Worker deployment, self-contained .NET 9 publish
- Per-item exception isolation in source parsers
- Adzuna key encryption (DPAPI)
- Word-boundary keyword matching

# Job Hunter — Todo

## Automation
- [ ] Scheduled headless runs — `--watch`/`SEARCH_INTERVAL_HOURS` so Worker loops without external cron
- [ ] CI: add Worker test project (WorkerConfigService env parsing)
- [ ] CI: verify Dockerfile builds (caught 3 local breaks that should've failed in CI)
- [ ] CI: dependency/secret scanning (Dependabot, gitleaks)
- [ ] Result notifications — webhook/email/Slack ping when headless run finishes

## Accuracy
- [ ] Use captured salary data (RemoteOK/Adzuna) in scoring/filtering — currently dead data

## UX
- [ ] First-run checklist/wizard
- [ ] Negative filters — exclude companies/keywords
- [ ] Progress bar during Gemini scoring batches

## Tests
- [ ] GeminiService — JSON parsing, retry/backoff, batch splitting
- [ ] ResumeParserService — PDF/DOCX extraction
- [ ] ApiJobSources.Dedup — more edge cases
- [ ] RunViewModel — extractable orchestration logic (skip-applied, etc.)

## Done
- [x] Resume profile extraction — parse once into structured skills/experience, stop resending raw text per batch
- [x] Gemini structured output — use `responseSchema` instead of manual JsonNode parsing
- [x] Fuzzy cross-source dedup — Levenshtein on normalized title (catches Adzuna re-posting Remotive listings)
- [x] Pre-flight validation — "Test connection" button for Gemini/Adzuna keys in Setup + multi-provider LLM fallback
- [x] HR consultant persona — Gemini prompt reframed as advisor, RedFlags field, "Consultant's Take"/"Fit" labels in UI + HTML report
- [x] Headless API sources (Remotive, RemoteOK, Arbeitnow, Adzuna)
- [x] Docker Worker deployment, self-contained .NET 9 publish
- [x] Per-item exception isolation in source parsers
- [x] Adzuna key encryption (DPAPI)
- [x] Word-boundary keyword matching
- [x] Deprecated Gemini model fix (gemini-flash-lite-latest)

# Job Hunter — Todo

## Automation

## Accuracy

## UX
- [x] First-run checklist/wizard
- [x] Negative filters — exclude companies/keywords
- [x] Progress bar during Gemini scoring batches

## Tests
- [x] GeminiService — JSON parsing, retry/backoff, batch splitting
- [x] ResumeParserService — file type validation
- [x] ApiJobSources.Dedup — edge cases (empty, single, exact dupes, fuzzy, case-insensitive, etc.)
- [ ] RunViewModel — extractable orchestration logic (skip-applied, etc.)

## Done
- [x] Scheduled headless runs — `SEARCH_INTERVAL_HOURS` so Worker loops without external cron
- [x] Result notifications — generic webhook POST (Slack/Discord-compatible payload) when headless run finishes
- [x] CI: dependency/secret scanning (Dependabot, gitleaks)
- [x] CI: verify Dockerfile builds (caught 3 local breaks that should've failed in CI)
- [x] CI: add Worker test project (WorkerConfigService env parsing)
- [x] Use captured salary data (RemoteOK/Adzuna) in scoring — fed into LLM prompt for the consultant take; no hard min-salary filter (free-text formats too inconsistent to parse reliably — revisit if a source gives structured min/max)
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

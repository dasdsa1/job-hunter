# Architecture

## Two front-ends, one core

- **JobHunterApp** (WPF, Windows) — interactive desktop app, browser-driven apply flow
- **JobHunterApp.Worker** (.NET 9, cross-platform) — headless, runs in Docker, API sources only
- Both share `JobHunterApp/Models` and `JobHunterApp/Services` via project-level `<Compile Include>`

## Job sources

`IJobSource` is the contract every source implements: `IsEnabled`, `FetchAsync`. Two families:

| Type | Sources | Needs browser? | Needs login? |
|---|---|---|---|
| Headless API | Remotive, RemoteOK, Arbeitnow, Adzuna | No | No |
| Browser scraper | LinkedIn, Indeed | Yes (Playwright) | Yes (one-time) |

`ApiJobSources.FetchAllAsync` runs all enabled API sources in parallel with per-source
failure isolation — one source erroring doesn't sink the run. Playwright only launches if
a browser site is actually selected (`RunViewModel.cs`), so headless-only runs never pay
for a browser.

Cross-source dedup (`ApiJobSources.Dedup`) runs in two passes: exact ID match, then
normalized `title|company` key (strips punctuation, lowercases) — catches the same job
reposted across boards.

## Scoring pipeline

1. CV parsed once per run (`ResumeParserService` — PDF via PdfPig, DOCX via OpenXML)
2. Jobs batched (20/batch) and sent to Gemini with the full resume text in each batch
3. Gemini returns score + advisory summary + red flags (`MatchResult`) — see
   [[AI-Scoring-Best-Practices]] for why it's framed as an HR consultant, not a classifier
4. Matches ≥ `MinScore` surface in the UI for selection

## Apply flow

API-source jobs (Remotive etc.) open in the default browser for manual apply — there's no
programmatic "apply" endpoint on aggregators. LinkedIn/Indeed use Playwright-driven
`IndeedApplicator`/`LinkedInApplicator` with human-in-the-loop review before submit.

## Config & secrets

`AppConfig` persists to `%LocalAppData%\JobHunter\config.json`. `ApiKey` and
`AdzunaAppKey` are DPAPI-encrypted on save (Windows only). The headless Worker reads
plaintext from env vars instead (`GEMINI_API_KEY`, `ADZUNA_APP_KEY`, etc.) since DPAPI
doesn't exist in Linux containers — see `WorkerConfigService.cs`.

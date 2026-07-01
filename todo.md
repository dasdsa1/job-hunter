# Job Hunter — Todo

## Phase 1 P0 (Weeks 1-2) — Finish & Integrate, Critical Issues

### P0 #5 — Resume Verification Pass (Post-generation)
- [ ] Add post-generation verification: compare every tailored-resume claim against source resume before showing to user.
- [ ] Unit test: source resume missing skill X → fabricated claim of X is flagged + not shown to user.
- [ ] Document source resume location + checksum in Curriculum for audit trail.
- [ ] **Why:** JobRight/Sonara's #1 complaint is hallucinated/fabricated skills; trust-eroding bug.

### P0 #6 — Human-Confirm Gate on Apply (No Full Auto-Submit)
- [ ] Hard gate: Submit action requires explicit user click (never auto-submit at scale).
- [ ] Code audit: verify no bypass code path to auto-submit.
- [ ] Test: submit requires user interaction; verify no code path skips user.
- [ ] **Why:** LazyApply-style full auto-submit → LinkedIn ban + recruiter distrust.

### P0 (Weeks 2-4 in parallel) — Structured CV Extraction + Integration
- [ ] **Structured Curriculum extraction** — stop sending raw résumé text per batch.
  - Parse résumé once per session → structured Curriculum (skills, experience, seniority, confidence-scored).
  - job-hunter matches against structured fields, not raw text.
  - `sourceText` bridge kept for verification fallback.
  - Ties to DocumentReader + platform-schemas CBIF Curriculum.
- [ ] Consume structured Curriculum: update `LlmServiceBase.MatchJobsAsync` to use Curriculum fields.
- [ ] Curriculum versioning: support re-upload → diff → resolve conflicts → new version.

## Phase 1 P1 (Weeks 3-8, In Parallel)

### P1 #17 — Prefer Public ATS APIs Over Scraping
- [ ] Query Greenhouse/Lever/Ashby/Workable public no-auth APIs where available.
- [ ] Sidesteps rate-limit/ban risk entirely for those ATSes.
- [ ] **Effort:** Medium (once per ATS, integrated into JobSource pipeline).

### P1 #18 — Ghost-Job Detection (Weighted Multi-Signal)
- [ ] Multi-signal scoring: posting age, repost frequency, vagueness.
- [ ] Surface as soft deprioritization (don't hard-reject).
- [ ] ~27-32% of postings are ghost jobs; no single signal reliable.
- [ ] **Effort:** Medium (signal engineering + confidence calibration).

### P1 (Weeks 4+, Blocked on marketplace-hub P0 #1-#2)
- [ ] Per-app P1 items deferred until this app's P0 ships + marketplace-hub outbox is verified.

## Phase 2 P2 (Trigger-Gated)

### P2 #31 — Never Hard-Delete Low-Scored Jobs
- [ ] Keep in "low match" filter; user can override.
- [ ] Triggered when auto-reject/auto-filter logic is touched.

## Tests
- [x] GeminiService — JSON parsing, retry/backoff, batch splitting
- [x] ResumeParserService — file type validation
- [x] ApiJobSources.Dedup — edge cases (empty, single, exact dupes, fuzzy, case-insensitive, etc.)
- [x] RunViewModel — extractable orchestration logic (skip-applied, etc.)

### New Tests (P0-P1)
- [ ] ResumeVerificationService — P0 #5 (fabric claim flagging)
- [ ] GeminiService + Curriculum consumption — P0 (structured CV extraction)
- [ ] SubmitViewModel / Apply gate — P0 #6 (human-confirm, no bypass)
- [ ] MatchingService with structured Curriculum — P1 (efficiency gains from structured data)

## Automation
- [ ] Scheduled `--watch` runs in Worker — currently one-shot; needs Hangfire integration.
- [ ] Webhook notification on run-complete (already scaffolded, needs testing).

## Accuracy
- [ ] Negative filters in RunViewModel (already UI-only; needs backend integration).
- [ ] Fuzzy-dedup via Levenshtein (already implemented, needs test coverage).

## UX
- [x] First-run checklist/wizard
- [x] Negative filters — exclude companies/keywords
- [x] Progress bar during Gemini scoring batches
- [ ] Show resume verification pass results to user (P0 #5).
- [ ] Submit button explicitly requires user click + confirmation (P0 #6).

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

# job-hunter — CLAUDE.md

## Stack (this repo)
- .NET 9: `JobHunterApp.Worker` (headless service), `JobHunterApp` (WPF desktop UI + shared Services/Models/ViewModels), `JobHunterApp.Tests` (xUnit).
- Parallel TS/Node CLI under `src/` (file-based, package.json at root) — separate tool, same repo.
- Docker: `Dockerfile` + `docker-compose.yml` build/run the Worker headless. Keep compose working after any change — verify with `docker compose build` when touching Worker/Dockerfile/csproj.
- No Angular frontend here (that's a different app in the platform — see below).

## Platform architecture (3 apps, polyrepo)
- job-hunter is ONE of three separate repos in a broader platform. Polyrepo, not monorepo — no cross-repo code sharing except via a shared semantically-versioned `contracts` package (NuGet/npm).
- Each app independently versioned and deployable. Integration is via MCP + a hub, not direct calls.
- Source of truth for platform-wide architecture: `../docs/marketplace-hub/` (external to this repo — consult, don't duplicate here).
- Known gap (do NOT build proactively, only if it's an explicit TODO item): job-hunter has no structured CV/curriculum model — resume is just a file path on disk. A future shared interchange format (via `contracts`) will formalize resume/candidate data. Don't invent this ahead of a TODO asking for it.

## Cross-cutting conventions (platform-wide, apply here where relevant)
- Async: hybrid — Postgres-backed outbox for background work, sync REST for human-facing requests. (job-hunter currently has no Postgres/outbox; this is the target pattern if/when persistence work is added here.)
- Scheduling: Hangfire is the platform standard for recurring/background jobs (relevant to the "scheduled headless runs" TODO).
- Auth: Google/Apple OAuth + CAPTCHA (not applicable to this repo's current desktop/worker scope unless a TODO adds a web-facing auth surface).
- i18n: pt-BR primary + en, via transloco (Angular convention — not applicable to WPF/Worker here).
- AI: pluggable provider abstraction, bring-your-own-key. This repo already does this via `LlmServiceFactory`/`FallbackLlmService` — keep new AI code behind that abstraction, never hardcode a single provider.

## Git workflow (strict)
- Use relative paths, never absolute, in code, scripts, configs, docs, and commands —
  repos and the `platform/` parent get moved/renamed; relative paths survive that,
  absolute ones don't.
- Self code-review BEFORE every commit — re-read your own diff for bugs, leftover debug code, and scope creep before committing.
- Break commits into logical chunks. No giant commits mixing unrelated concerns.
- Conventional Commits format (`feat:`, `fix:`, `test:`, `chore:`, `ci:`, `docs:`, `refactor:`).
- Keep `docker-compose.yml` / Worker Docker build working — don't merge a change that breaks the container build.

## Testing
- Every TODO item needs a verification step (test run or build) before being marked done — no exceptions.
- xUnit tests live in `JobHunterApp.Tests`. Run via `dotnet test`.

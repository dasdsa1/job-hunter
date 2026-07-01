# ADR 0001 — Local Persistence + Hub Sync for Standalone Job Hunter

**Status:** Proposed
**Date:** 2026-07-01
**Context repos:** job-hunter (this), marketplace-hub, platform-schemas

## Context

Job Hunter runs as a standalone desktop app today (WPF) and is a candidate for mobile.
Persistence is currently a set of loose JSON files under `%LocalAppData%/JobHunter/`
(`config.json`, `applied_jobs.json`, `search_history.json`, `resume_cache.json`) via
`FileConfigService` / `AppliedJobsService`, plus on-disk report/CV/letter directories
(`AppPaths`).

Two forces pull in different directions:

1. **Standalone UX** — the app must work fully offline, start instantly, and keep the
   user's data on their device (résumé, applications, scores are sensitive).
2. **Platform learning loop** — per the platform charter, job-hunter owns the most mature
   `request → responses → selection → fulfillment` pipeline and the **AiExtractionAccuracy**
   feedback signal. That signal is only valuable if it reaches the hub for aggregation.

The naive "just use SQLite" answer solves (1) but strands the data on-device, defeating (2).
The naive "just use the hub's Postgres" answer breaks offline and centralizes PII we don't
need to centralize.

## Decision

**Local-first SQLite as the device system-of-record, plus a client-side outbox that pushes
a privacy-filtered subset to the marketplace-hub.** Mirror the pattern the hub already uses
internally (`OutboxMessage` → `OutboxRelayJob`), on the client side.

### Layer 1 — Device store: SQLite

Replace the scattered JSON files with a single SQLite DB at `%LocalAppData%/JobHunter/jobhunter.db`
(iOS: app sandbox `Documents/`, Android: `getDatabasePath()`), accessed via
`Microsoft.Data.Sqlite` + Dapper (thin) — not EF Core on the client (EF Core is the hub's
choice; the client wants a small, fast, migration-simple footprint).

Rationale over alternatives:
- **vs JSON files** — queryable ("jobs scored ≥8 last 30d"), transactional, no full-file
  rewrite per change, concurrency-safe (Worker + UI can both touch it).
- **vs Realm/Core Data/Room** — SQLite is the common substrate under all of them and is
  portable across .NET desktop, .NET MAUI mobile, and the Node CLI in `src/`. No vendor
  lock-in; the file is user-backupable.
- **vs LiteDB** — SQLite has FTS5 (résumé/job full-text search later) and universal tooling.

Minimal schema (v1 — mirrors platform-schemas entity names so sync is a straight map):

```sql
-- device system-of-record
CREATE TABLE curriculum (
    id            TEXT PRIMARY KEY,      -- local uuid
    client_ref    TEXT UNIQUE,           -- stable idempotency key for hub
    payload_json  TEXT NOT NULL,         -- CBIF curriculum-1.0.0
    source_path   TEXT,                  -- original résumé file on disk
    source_sha256 TEXT,                  -- audit trail (TODO P0 #5)
    version       INTEGER NOT NULL DEFAULT 1,
    updated_at    TEXT NOT NULL
);

CREATE TABLE job_posting (
    id           TEXT PRIMARY KEY,
    client_ref   TEXT UNIQUE,
    payload_json TEXT NOT NULL,          -- CBIF job-posting-1.0.0
    source       TEXT,                   -- remotive | adzuna | linkedin | ...
    fetched_at   TEXT NOT NULL
);

CREATE TABLE match (
    id             TEXT PRIMARY KEY,
    client_ref     TEXT UNIQUE,
    curriculum_id  TEXT NOT NULL REFERENCES curriculum(id),
    job_posting_id TEXT NOT NULL REFERENCES job_posting(id),
    score          INTEGER NOT NULL,     -- 1-10 local; ×10 → 0-100 on sync
    payload_json   TEXT NOT NULL,        -- CBIF match-1.0.0 (reasons, redFlags, extraction)
    created_at     TEXT NOT NULL
);

CREATE TABLE application (
    id             TEXT PRIMARY KEY,
    client_ref     TEXT UNIQUE,
    job_posting_id TEXT NOT NULL,
    status         TEXT NOT NULL,        -- applied | skipped | draft
    payload_json   TEXT NOT NULL,        -- CBIF application-1.0.0
    applied_at     TEXT
);

-- client-side outbox (identical intent to hub's OutboxMessage)
CREATE TABLE outbox (
    id             TEXT PRIMARY KEY,
    entity_type    TEXT NOT NULL,        -- curricula | jobs | matches | applications
    entity_id      TEXT NOT NULL,
    payload_json   TEXT NOT NULL,        -- exactly what gets POSTed
    created_at     TEXT NOT NULL,
    processed_at   TEXT,
    attempts       INTEGER NOT NULL DEFAULT 0,
    last_error     TEXT
);

CREATE INDEX ix_outbox_unprocessed ON outbox(processed_at) WHERE processed_at IS NULL;
```

`config.json` (settings + DPAPI-encrypted secrets) **stays a file** — it's not entity data,
it's app config, and DPAPI/keychain semantics differ per platform. Don't put secrets in the DB.

### Layer 2 — Client outbox → hub

Every entity write also inserts an `outbox` row **in the same SQLite transaction** (this is
the whole point of the outbox pattern: the data and the intent-to-sync commit atomically, so
a crash can't leave them inconsistent). A background relay drains it:

1. `SELECT * FROM outbox WHERE processed_at IS NULL ORDER BY created_at LIMIT n`
2. `POST /v1/{entity}` to the hub with the CBIF payload, `Authorization: Bearer <token>`,
   `X-CBIF-Version: 1.0.0`. The hub is **idempotent on `clientRef`**, so re-sending after a
   failed-but-actually-succeeded POST is safe (returns `wasReplay`).
3. On 201/207 → `processed_at = now`. On failure → `attempts++`, `last_error`, exponential
   backoff; after N attempts leave it (client-side DLQ = just unprocessed rows to inspect).

In the WPF app the relay is a hosted background task; on mobile it's a periodic background
job. In the headless Worker it runs at end-of-run. No message broker — same "not yet" stance
the hub itself takes.

### What syncs vs what stays local (privacy filter)

| Data | Local (SQLite) | Synced to hub | Notes |
|------|:--:|:--:|---|
| Match scores, reasons, redFlags | ✓ | ✓ | Core learning signal |
| Extraction metadata (`confidence`, `isCorrect`, `fieldConfidences`) | ✓ | ✓ | **The AiExtractionAccuracy signal** |
| Job postings (public data) | ✓ | ✓ | De-dups across users on hub |
| Curriculum **structure** (skills, seniority, titles) | ✓ | ✓ (opt-in) | Structured fields, not raw text |
| Curriculum `sourceText` / raw résumé | ✓ | ✗ | Never leaves device by default |
| `basics` PII (name, email, phone) | ✓ | ✗ | Stripped before POST; hub keys on `clientRef` |
| API keys / secrets | file (DPAPI) | ✗ | Bring-your-own-key stays on device |

Sync is **opt-in** and defaults off until the user consents (aligns with the AI-hiring-ethics
posture already in the wiki). The hub tenant/token scoping (`curriculum:write`, `matches:write`,
etc.) enforces least privilege even when on.

## Consequences

**Positive**
- Offline-first, instant start, user owns their data — standalone UX intact.
- The valuable signal (match quality + extraction accuracy) reaches the platform learning
  loop without centralizing résumés or PII.
- Wire format is already-defined CBIF contracts → sync is a field map, not a new protocol.
- Client outbox mirrors the hub's proven pattern; idempotent POST makes retries trivially safe.
- Unblocks TODO items that assume structured data (P0 structured-Curriculum, ghost-job signals
  needing history, resume-verification audit trail via `source_sha256`).

**Negative / costs**
- SQLite migration layer to own on the client (mitigate: keep it Dapper + hand-written
  migrations, versioned in one table; don't pull in EF Core).
- One-time import of existing JSON files → SQLite (write a `JsonToSqliteMigrator`, run once on
  upgrade, keep JSON as fallback for one release).
- Sync introduces auth/token handling on the client (hub Bearer token provisioning) — gated
  behind the opt-in so it's not on the critical path for v1.

## Rollout (incremental, each step shippable)

1. Add SQLite + schema + `IDeviceStore` (Dapper). Dual-write JSON **and** SQLite; read from SQLite. Ship.
2. One-shot `JsonToSqliteMigrator` on upgrade; drop JSON writes next release.
3. Add `outbox` table + writes (no relay yet — just accumulate). Ship.
4. Add opt-in consent UI + hub token config in Setup. Ship.
5. Add the relay (background push). Ship. Learning loop live.

Steps 1–3 are pure local value (queryability, history) and carry zero platform/network risk —
they can land regardless of hub readiness. Steps 4–5 are gated on hub token provisioning
(currently a STOP item: needs secrets/accounts).

## Related

- Hub ingestion contract: `marketplace-hub/Marketplace.Hub.Api/EntitiesController.cs`
- Outbox reference: `marketplace-hub/Marketplace.Hub.Core/OutboxMessage.cs`
- Wire schemas: `platform-schemas/schemas/{curriculum,match,job-posting,application,extraction-metadata}-1.0.0.json`
- [[wpf-testability-deployment-onboarding]] — the deployment surface this data flows through

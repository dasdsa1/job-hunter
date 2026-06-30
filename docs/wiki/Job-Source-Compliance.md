# Job Source Compliance — Why We're API-First

## The risk with LinkedIn/Indeed automation

Both explicitly prohibit automated scraping in their Terms of Service. 2026 field testing
shows real consequences: independent stress tests of LinkedIn scrapers found roughly 3 of
5 tools got accounts flagged or throttled within ~50 saves. Indeed is more tolerant in
practice (public job feeds exist, less aggressive enforcement) but still forbids bot
access in its ToS.

Legally, the Ninth Circuit (*hiQ v. LinkedIn*) found scraping *publicly visible* data
doesn't violate the CFAA, but the Supreme Court later vacated that ruling on remand —
ToS violations and account-level enforcement remain real risk even where criminal
liability doesn't attach. Bottom line: **scraping LinkedIn/Indeed risks the user's own
account**, not just a legal abstraction.

## Our mitigation: headless API sources by default

Remotive, RemoteOK, Arbeitnow, and Adzuna are added specifically because they're
**public, documented, zero/low-auth APIs** built for this — no ToS violation, no account
risk, no browser fingerprinting cat-and-mouse. This is why the headless Worker
(`JobHunterApp.Worker`) defaults to these sources and only launches Playwright if the
user explicitly opts into a browser site.

LinkedIn/Indeed scraping remains available because some users want it and accept the
risk knowingly — but it's opt-in, requires the user's own logged-in session (we don't
bypass auth or impersonate), and every apply action requires human review
(`ReviewRequest` in `RunViewModel`). We don't auto-submit anything on LinkedIn/Indeed
without a confirm click.

## What we deliberately don't do

- No credential harvesting or bypassing LinkedIn/Indeed login — uses the user's own
  authenticated browser session (Playwright with a persistent profile)
- No CAPTCHA solving or bot-detection evasion
- No headless browser spoofing beyond a standard User-Agent header on API calls
  (`SourceHelpers.CreateClient` — required because RemoteOK rejects default .NET UA strings,
  not for evasion)
- No mass/bulk scraping beyond `MaxJobsPerSite` (user-configured, defaults to 20)

## If LinkedIn changes posture

LinkedIn has a "Vetted" official API program (restrictive, requires application). If
LinkedIn integration ever needs to scale beyond personal use, that's the only fully
compliant path — not a goal for this project today, since it's a personal job-search
tool, not a sourcing platform.

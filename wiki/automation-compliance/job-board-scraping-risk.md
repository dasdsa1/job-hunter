# Job Board Scraping Risk (LinkedIn / Indeed)

**Sources:** ConnectSafely (2026); PhantomBuster (2025); Tracker-RMS (2026); Mantiks (2026); DEV Community / agenthustler (2026); JobBoardly (2025); ApplyArc (2026)
**Raw:** [linkedin-indeed-scraping-risk.md](../../raw/job-board-scraping/2026-07-01-linkedin-indeed-scraping-risk.md)
**Updated:** 2026-07-01

## Summary

LinkedIn and Indeed both prohibit automated scraping in their ToS, but enforcement
severity and legal exposure differ meaningfully between them — and between "scraping for
data collection" and "automating a logged-in user's own browsing session," which is the
distinction that shapes how a personal job-search tool should be built.

## LinkedIn: high enforcement risk

Cited 23% ban rate for third-party automation tools. A June 2026 stress test of 5
scrapers across 200 real job pulls found 3 got accounts flagged or throttled after
roughly 50 saves — concrete, recent evidence the enforcement is active and effective, not
just a ToS clause nobody checks.

Legal backdrop: *hiQ Labs v. LinkedIn* (9th Circuit) initially found that scraping
publicly visible data doesn't violate the CFAA (no unauthorized access where there's no
access control to bypass). The U.S. Supreme Court later vacated that ruling on remand,
which read as support for LinkedIn's ability to enforce its ToS through other means
(contract/account-level enforcement) even where federal computer-crime law doesn't apply.
Net effect: **ToS violation risk to the user's own account is real and current**,
independent of the CFAA question being murkier than it first appears.

## Indeed: lower but nonzero risk

Indeed's ToS also forbids bot/scraping access, but enforcement in practice is described
as more lenient — Indeed has historically tolerated more scraper activity, and even
exposes a public job feed in places. Still not a green light; the prohibition is explicit
in the ToS text.

## The compliance-by-architecture pattern

The structural fix isn't "scrape more carefully" — it's **don't scrape sites that
prohibit it; prefer sites that publish a documented public API for exactly this use
case.** Job aggregator APIs (Remotive, RemoteOK, Arbeitnow, Adzuna) exist specifically to
be consumed programmatically — using them carries none of the ToS/account risk that
scraping LinkedIn/Indeed does, because there's no prohibition to violate.

For job boards where no API exists and scraping is the only option (LinkedIn, Indeed),
the lower-risk pattern is: automate the *user's own already-authenticated browser
session* (no credential bypass, no impersonation) rather than building a stealth scraper
designed to evade detection. This doesn't eliminate ToS risk, but it avoids stacking
ToS-violation risk on top of credential-theft/bot-detection-evasion risk.

## Application: Job Hunter project

The headless Worker (`JobHunterApp.Worker`) defaults to API sources only
(Remotive/RemoteOK/Arbeitnow) and only launches a Playwright browser if the user
explicitly opts into LinkedIn/Indeed in the GUI app — this is the compliance-by-default
posture described above. LinkedIn/Indeed automation uses the user's own logged-in browser
profile (`BrowserService` with a persistent Playwright profile), not credential bypass or
bot-detection evasion, and every apply action requires human confirmation before
submission. The risk that remains (account flagging from automation patterns LinkedIn
detects regardless of credential legitimacy) is accepted knowingly by the user opting
into those sources, not hidden from them.

## See Also
- [LLM Resume/Job Matching](../llm-engineering/llm-resume-job-matching.md) — the scoring layer that runs on whatever jobs get fetched, regardless of source

# Job Hunter Wiki

Job Hunter is a desktop app (+ optional headless Docker worker) that scrapes/fetches job
listings, scores them against your resume with Gemini, and helps you apply — fast.

## Pages

- [[Architecture]] — how the WPF app, headless Worker, and job sources fit together
- [[AI-Scoring-Best-Practices]] — how matching/scoring is designed, and the research behind it
- [[Job-Source-Compliance]] — why we're API-first, and the LinkedIn/Indeed scraping risk we accept
- [[Roadmap]] — what's planned next (mirrors `todo.md` in the repo)

## Quick start

1. Setup tab → add your Gemini API key, upload your CV
2. Search tab → pick job sources (API sources need no login; LinkedIn/Indeed need a one-time browser login)
3. Run tab → review AI-scored matches, apply

See `DOCKER.md` in the repo root for headless/scheduled deployment.

# WPF Testability, EC2 Deployment, and Onboarding UX — Applied to Job Hunter

**Sources:** Pluralsight WPF/MVVM TDD course; Microsoft Learn "Writing a Testable Presentation Layer with MVVM"; MVVM Revisited arXiv paper (2025); AWS "Deciding where to host .NET applications on AWS"; Depot "Optimal Dockerfile for .NET Worker Service"; UserGuiding/Appcues/UX Design Institute onboarding UX guides (2025-2026)
**Updated:** 2026-07-01

## Summary

Research on three gaps identified in the current TODO list: `RunViewModel` untestability
(blocked TODO item), EC2 deployment patterns for the headless Worker, and onboarding
checklist UX — applied against what this repo currently does.

## Abstract the dispatcher to unblock RunViewModel tests

The standard fix for "ViewModel calls `Application.Current.Dispatcher.Invoke` so I can't
unit test it" is not to strip the threading marshaling out — it's to hide it behind a
thin interface (`IDispatcher` with `Invoke`/`InvokeAsync`) injected into the ViewModel via
constructor. Production code wires up the real WPF dispatcher; tests inject a synchronous
fake that just runs the delegate inline. Once notifications are guaranteed to happen
through this seam, the ViewModel's orchestration logic (skip-applied filtering, batch
progress reporting, step transitions) becomes plain testable C#, no UI thread needed.

**Applied:** Not yet. `RunViewModel` currently calls `Application.Current.Dispatcher.Invoke`
directly in ~4 places (job list updates, cover letter streaming). This is the concrete
unblock for the still-open "RunViewModel — extractable orchestration logic" TODO item:
introduce `IDispatcher`, inject a real implementation in `App.xaml.cs`/DI setup, inject a
synchronous test double in `JobHunterApp.Tests`. Estimated small — the orchestration logic
itself (skip-applied, scoring loop, step machine) is already fairly separable; only the
`Application.Current` static touches need the seam.

## EC2 Spot Instances fit this workload well

AWS guidance highlights EC2 Spot Instances (up to 90% cheaper than on-demand) as
appropriate specifically for "fault-tolerant" batch jobs — a run that gets interrupted can
just restart from scratch with no data loss, which describes the headless Worker exactly
(it's already designed to run-once-and-exit per `docker-compose.yml`'s `restart: "no"`).

**Applied:** Not yet — `DEPLOYMENT.md` currently assumes a standard on-demand EC2 instance.
Given the Worker is idempotent and non-critical-path (a missed nightly run just means
scoring resumes next cycle), Spot is a low-risk cost win worth calling out in the
deployment doc as the recommended default, not just an option.

## Docker image tagging: avoid `:latest` in production

Docker best-practice guidance for 2026 is consistent: tag images with semver or a
timestamp/commit SHA rather than relying on `:latest`, because `:latest` makes rollback
and reproducibility ambiguous — you can't tell which build is actually running.

**Applied:** Not yet — `scripts/deploy-to-ec2.sh` and `DEPLOYMENT.md` currently tag
everything `job-hunter-worker:latest`. Low-effort fix: tag with the git short SHA
(`docker build -t job-hunter-worker:$(git rev-parse --short HEAD)`) alongside `:latest`,
so a specific EC2 deployment can be pinned and rolled back deterministically.

## Checklists pair with progress indicators to drive completion

Onboarding UX research is consistent that a static checklist alone is weaker than one
paired with a visible progress signal (checkmarks per item, or a completion percentage) —
users are more likely to finish the last step when they can see how close they are.

**Applied:** Partially. The current "Getting Started" section in `SetupView.xaml` shows
the 3 setup steps as static text with no per-step completion state — a regression from an
earlier draft in this session that had per-item checkmarks but was simplified away for
XAML complexity. A lighter version (reusing the existing `MissingSetupItems` binding that
already tracks CV/API-key completion) could add a green checkmark next to "1. Select CV"
and "2. Add AI provider key" once each is satisfied, without the multi-trigger XAML
overhead of the earlier draft.

## Cross-references

- [[llm-resume-job-matching]] — the scoring pipeline these deployment/UX improvements sit around.

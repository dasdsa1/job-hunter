# AI Hiring Ethics and Regulation

**Sources:** Scale.jobs (2026); Phenom (2026); AssessCandidates (2026); TechClass (2026); Journal of Business Ethics/Springer Nature (2022); Cadient (2026); eSkill (2026); OneReach (2026); Hydrogen Group (2026); TechTarget (2026)
**Raw:** [ai-hiring-automation-ethics-regulation.md](../../raw/ai-hiring-ethics/2026-07-01-ai-hiring-automation-ethics-regulation.md)
**Updated:** 2026-07-01

## Summary

The regulatory and ethical landscape for AI in hiring decisions, and what it means for a
candidate-side AI job-search tool specifically (a meaningfully different risk profile
than employer-side AI screening).

## The asymmetry: employer-side screening vs. candidate-side search tools

Most regulation and litigation targets AI used *by employers* to screen/reject
candidates — that's where disparate-impact liability lives. Colorado's AI Act (effective
June 2026), California's ADS Regulations (effective October 2025), and NYC's bias-audit
law (audit required before use, 10 business days' notice to candidates) all regulate
employer-deployed decision tools.

A tool that scores jobs *for* the candidate's own benefit — helping them prioritize where
to apply — sits outside this regulatory target, since no rejection decision is being
automated against a third party. The ethical stakes are lower but not zero: the model's
own blind spots (e.g., undervaluing non-traditional career paths) still shape what the
candidate sees as "worth applying to."

## Workday/Mobley case — why "neutral vendor" framing doesn't hold up

*Derek Mobley v. Workday*: plaintiff alleged Workday's AI screening tools
disproportionately rejected applicants who were older, Black, or disabled. The court
certified a class under the ADEA (Age Discrimination in Employment Act) — signaling
courts are willing to treat AI vendors as "agents" of the hiring employer, not neutral
infrastructure providers immune from discrimination claims. Relevant precedent for anyone
building hiring-adjacent AI tooling, even candidate-side: it establishes that "the model
made the call, not us" is not a reliable legal shield.

## Human-in-the-loop is the consistent thread across all guidance

Every source converges on the same practice regardless of regulatory jurisdiction: a
human must retain the final decision-making authority, with the AI providing input/scores
rather than autonomous action. Specific recurring recommendations:

- Manual review path required when AI has known accuracy limitations for certain groups
  (non-native speakers, candidates with disabilities)
- Pilot/test for bias before deployment, monitor continuously after
- Be transparent with the people affected about how AI is used and what happens to their
  data
- Establish accountability structures (ethics committees, escalation paths) before
  scaling usage

## Application: Job Hunter project

Every apply action in this codebase requires explicit human confirmation
(`RunViewModel.ReviewRequest`/`CvChoiceRequest`/`LetterChoiceRequest`) — nothing is
submitted without a human clicking through. This satisfies the human-in-the-loop
principle by construction, not as an afterthought.

Bias caveat still applies even on the candidate-side: the AI's judgment of "good fit"
(`MatchResult.Score`/`Summary`) reflects the model's own training-data blind spots. No
mitigation is implemented for this — documented as a known, unaddressed limitation rather
than treating "we're not the employer" as a full pass.

## See Also
- [LLM Resume/Job Matching](../llm-engineering/llm-resume-job-matching.md) — the scoring mechanism this governance applies to

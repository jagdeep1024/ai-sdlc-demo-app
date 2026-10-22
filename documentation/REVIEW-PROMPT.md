# PR review prompt

The workshop-specific prompt was unavailable. Jagdeep authorized this structured standard fallback on 22 October 2026. It is the chosen prompt, not a reproduction of the workshop original.

## Exact prompt

```text
Review this pull request against the assignment specification and AGENTS.md. Inspect the entire diff and relevant surrounding code.

Check approval thresholds, distinct-user enforcement, Director-only second approval, server-session authority, unchanged invoice state after rejected attempts, and concurrent operations.

Review whether unit tests catch realistic mistakes, whether the Playwright test genuinely exercises the UI rejection, and whether mutation results are comparable and accurately reported.

Identify correctness defects, regressions, missing tests, and misleading documentation. For each actionable finding, provide a stable ID, severity, file/line, failure scenario, and recommended correction. Separate findings from optional suggestions. Do not invent findings to fill a quota. Do not modify files.
```

Review records and verdicts are in REVIEW-FINDINGS.md. The initial review targets PR #1 at head `68995db` against base `66cb3f9`. Findings refer to that reviewed state; fixes and any follow-up verification are recorded separately.

# AI in SDLC — invoice approval limits

[Pull request #1](https://github.com/jagdeep1024/ai-sdlc-demo-app/pull/1) adds role-based approval limits to the workshop invoice demo. This is a public fork of [NITIN1670/ai-sdlc-demo-app](https://github.com/NITIN1670/ai-sdlc-demo-app). The PR targets this fork's main, retains upstream history, and stays open for evaluation.

**Submission status:** implementation and automated verification are complete. PR-review acceptance and the recorded video are pending. **Video link: not yet available — recording/upload required.** The assignment is not represented as fully submitted.

## Behavior and assumptions

| Invoice amount | Approval required |
| --- | --- |
| Up to 50,000 inclusive | Manager or Director |
| Above 50,000 through 500,000 inclusive | Director |
| Above 500,000 | Manager or Director first; a different Director second |

The first high-value approval is recorded while the invoice remains pending. Final decision fields are set only after a distinct Director completes approval. The same authenticated username never counts twice, even across separate sessions. Rejected attempts return a clear reason, preserve every invoice field and perform no repository write.

The first high-value approver may be either Manager or Director; this resolves an unspecified part of the brief. Usernames are the case-sensitive configured identities used by demo sign-in. The existing demo.approver account is Manager; demo.director and demo.director2 are distinct Directors. Admin retains rejection/deletion permissions but cannot approve. Existing USD display and decimal amounts are retained. Legacy approved seed invoices retain their old decision metadata without invented approval history.

## Specification and context

OpenSpec 1.14.3 produced the change scaffold and artifact instructions for the [proposal, design, specification and staged tasks](openspec/changes/role-based-approval-limits/). The workflow used openspec init, new change, instructions and strict validation; generated Codex skills and the artifacts are committed. The specification explicitly covers boundaries, distinct identities, full-state failure invariants, session authority and concurrent transitions.

[AGENTS.md](AGENTS.md) tells a cold agent where domain rules, HTTP/session code, storage, UI and tests belong; it includes exact commands, approval rules and review expectations. It influenced the actual work: domain tests were consolidated to match its naming convention, role enforcement stayed in Domain, and browser checks use page objects with accessible locators. Reviews challenged weak exception assertions and concurrency tests whose worker starts could be mostly serial; these were strengthened before delivery.

The custom [assignment-evidence skill](.agents/skills/assignment-evidence/SKILL.md) requires consistent Objective, Changes, Verification, Findings and Pending decisions reporting. Project [hooks](.codex/hooks.json) feed a redacting structured logger; the readable log is generated from JSONL. Native prompt/completion delivery was verified in a separate normally trusted read-only Codex session. Explicit orchestration capture was also used; no claim of complete automatic capture of earlier desktop history is made. [EVIDENCE-WORKFLOW.md](documentation/EVIDENCE-WORKFLOW.md) states the capture boundaries and redaction behavior.

## Implementation and staged commits

Domain ApprovalService owns lifecycle and approval rules. API authorization derives identity/role from the authenticated server session; request body fields cannot override them. Immutable entries retain approver username, role and UTC time.

The singleton repository's Transition operation locks the entire read/validate/change/commit sequence across scoped services. It works on a detached invoice and replaces stored state only after success. Reads, additions and deletion share synchronization and return copied snapshots, preventing partial state exposure, lost updates, approval/delete resurrection and history-reference leaks. This deliberately uses one store lock for the small in-memory demo.

| Commit | What changed and why |
| --- | --- |
| 7dea147 | Context, OpenSpec and evidence workflow before implementation |
| 4af9832 | Single-approver limits and tests; safely denied high-value approval until the next stage |
| 1f457d9 | Separate dual-approval implementation, history, atomic transitions, UI and reviewed tests |
| 51e8a60 | Real browser rejection, mutation-gap tests, measurements and CI |
| b14a712 | Corrected a real Linux logger-test failure without weakening redaction assertions |
| 68995db | Recorded successful hosted verification |

The single-approval and dual-approval stages used separate implementation prompts and commits. Subsequent review/write-up work is recorded in the PR history rather than rewriting those checkpoints.

## Tests and verified outcomes

- Original baseline: 7 xUnit cases passed. Single-approval stage: 45; dual-approval stage: 73. Nine justified mutation-gap cases brought the full .NET suite to 82.
- Domain/API tests cover inclusive thresholds and immediately adjacent values, both high-value first roles, Director-only second approval, duplicate identity across sessions, body-forged authority, invalid lifecycle/roles, complete unchanged snapshots, and no failed write.
- Concurrent service/HTTP cases cover duplicate and distinct approvals, competing approval/rejection, deletion, and reads observing consistent detached snapshots. History-array and returned-collection mutation are tested.
- One [Playwright scenario](e2e/tests/approval-limits.spec.ts) signs in Manager, creates/submits 50,000.01 through UI controls, attempts approval, checks the visible error, reloads, and compares the entire invoice with its pre-attempt snapshot. It uses no mocks or API-created setup and starts an isolated app on 5097.
- Independent reviewers examined generated tests and browser assertions; the formal PR reviewer independently reran 82 .NET cases, one Chromium case, five logger tests, TypeScript and strict OpenSpec successfully.
- [Hosted CI](https://github.com/jagdeep1024/ai-sdlc-demo-app/actions/runs/39728576732) passed verification and mutation jobs. It runs on feature pushes/PRs, with read-only repository permissions and downloadable browser/mutation artifacts.

A first browser attempt failed while Chromium installation was incomplete, then passed after installation. The first hosted verification run exposed CI-01, a logger-test dependency on directory iteration order and the empty lockfile. The correction explicitly tests both published outputs; subsequent hosted runs passed. These failures and corrections are retained in the evidence.

## Mutation scores

Actual Stryker 4.13.0 measurements with identical production code, mutation configuration and scope:

```text
Before: 89.09% = 49 killed / 55 scored
       4 survived, 2 no coverage, 6 compile errors, 38 ignored
       40 selected ApprovalServiceTests cases
After:  94.55% = 52 killed / 55 scored
       1 survived, 2 no coverage, 6 compile errors, 38 ignored
       49 selected ApprovalServiceTests cases
Gain:  5.46 percentage points; three additional faults detected
```

Both generated 99 mutants; no timeouts occurred. Ignored/compile-error mutants are excluded from the score, while no-coverage mutants remain undetected in the denominator. Scope is ApprovalService.cs at Standard level with ApprovalServiceTests; this is not a whole-application mutation score.

IDs 4, 67, 69 exposed missing clear-error/rejection-lifecycle assertions and were killed by three new theory methods. All 17 production C# hashes and the config hash match before/after. Independent review verified sources, common tests, mutation definitions and the three actual status changes.

Remaining IDs 29/32/33 concern PendingApproval with two stored approvals. Valid API completion atomically sets Approved, so subsequent attempts fail the earlier pending-state check. Manually seeded inconsistent data can still make those mutations observable: they are not claimed globally equivalent. They remain disclosed and included in 94.55%; no exclusions or artificial tests were added to manufacture 100%. [Counts, source hashes, complete normalized reports and commands](documentation/MUTATION-RESULTS.md) are committed. Hosted Linux CI reproduced 94.55%.

## PR review and judgement

The original workshop-specific prompt could not be found. Jagdeep authorized the [recorded structured standard fallback](documentation/REVIEW-PROMPT.md); it is identified as a fallback rather than the workshop original. The independent review inspected the actual PR diff at `68995db` against base `66cb3f9`, surrounding implementation/tests/CI and machine evidence. Its [original report](documentation/PR-REVIEW.md) found no correctness blockers.

| Finding | Verdict | Reason |
| --- | --- | --- |
| PR-S01: optional high-value two-account browser scenario | Deferred | Domain/API/concurrency tests plus actual live UI checks cover the sequence; a second automated browser scenario is a future UI regression enhancement. The required rejected-over-limit scenario is implemented. |
| PR-D01: missing retained observation for linked newer CI run | Fixed | Verified GitHub metadata and retained the actual successful result in the structured log |
| PR-D02: spacing in newly authored prose | Fixed | Copy-edited the write-up/review metadata without altering original reports |
| TR-S1-01: missing rejection identity assertion | Fixed | Added and independently reviewed; passing suite |
| TR-S2-01: missing history-array/collection mutation assertions | Fixed | Added and independently verified; passing suite |
| CI-01: directory-order-dependent redaction test | Fixed | Explicitly tests both published files; independent review and Linux CI passed |

PR-S01 is the optional PR-review suggestion; PR-D01 and PR-D02 are documentation-addendum suggestions; TR-S1-01, TR-S2-01 and CI-01 are earlier development/test findings, included for traceability. These are agent dispositions; Jagdeep's PR-review checkpoint is pending. [REVIEW-FINDINGS.md](documentation/REVIEW-FINDINGS.md) distinguishes findings, mutation observations and human acceptance. No issue was invented to fill a review quota.

## AI interaction evidence

Codex was used for implementation and review. A main coordinator, persistent implementation agent, evidence-tooling agent, independent test-review sub-agent, mutation reviewer and separate PR reviewer performed real tasks. Human stage clearances and agent-authored delegations are identified separately.

The [structured interaction source](documentation/ai-interactions.jsonl) and [readable log](documentation/AI-INTERACTION-LOG.md) retain actual task prompts, scoped excerpts where labelled, original review reports, real corrections and verification outcomes. Retrospective summaries are labelled. [Test-review evidence](documentation/TEST-REVIEW.md) and [browser/CI evidence](documentation/BROWSER-TEST-REVIEW.md) supplement the log. No fabricated interaction, human judgement or video is presented as executed.

## Reproduce

Install the .NET 8 SDK selected by global.json and Node 24. Then, from repository root:

```sh
dotnet test backend/InvoiceApp.sln
npm ci
npm run spec:validate
npm run evidence:test
```

Browser verification:

```sh
cd e2e
npm ci
npx playwright install chromium
npm run typecheck
npm test
```

Mutation verification from repository root:

```sh
dotnet tool restore
cd backend/tests/InvoiceApp.Tests
dotnet stryker --skip-version-check
```

Run the manual UI with dotnet run --project backend/src/InvoiceApp.Api and open localhost:5000. The README lists public local demo credentials. No database or external deployment is needed; invoices and sessions reset on restart. The final recorded 8–10-minute MP4 and accessible video link still need to be supplied.

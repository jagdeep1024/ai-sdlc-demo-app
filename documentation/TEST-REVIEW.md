# Test review

Baseline: seven existing xUnit tests passed before implementation.

Current checkpoint: Jagdeep authorized stage two with “Okay, let's implement the next stage.” The stage-one section below records its earlier review state; that checkpoint is now cleared. Stage-two verification passed 73 cases. Stage-two human acceptance and the later workshop PR review have not yet occurred.

## Independent stage-one review

Reviewer: `/root/implementation/test_review`, read-only delegated review. The original report is captured in the interaction log.

Objective

Independently review stage-one tests, domain/API changes, and rejection invariants against `AGENTS.md`, the assignment skill, and OpenSpec.

Changes

None. Read-only review completed after the boundary tests were consolidated into `ApprovalServiceTests.cs`.

Verification

Executed:

`/Users/jagcoool/.dotnet/dotnet test backend/InvoiceApp.sln --no-restore --verbosity quiet`

Result: **45 passed, 0 failed, 0 skipped**.

Every test method reviewed:

| Test method | Defect caught / assertion assessment |
|---|---|
| `Submit_MovesDraftToPendingApproval` | Missing draft transition; existing focused regression check. |
| `Submit_RejectsInvoiceThatIsNotADraft` | Invalid lifecycle submission; existing exception check. |
| `Approve_SetsStatusApproverAndTimestamp` | Missing approval status, identity, or timestamp; updated signature exercised. |
| `Approve_RejectsInvoiceThatIsStillADraft` | Approving drafts; stronger snapshot coverage exists below. |
| `Approve_ThrowsWhenInvoiceDoesNotExist` | Missing-invoice handling; stronger write-count coverage exists below. |
| `Reject_RecordsReasonAndApprover` | Rejection status/reason regression; review found the identity assertion missing and it was added under TR-S1-01. |
| `Reject_RequiresAReason` | Accepting blank rejection reason; existing exception check. |
| `Approve_AllowedBoundaries_RecordAuthenticatedDecision` | Incorrect inclusive limits, role restrictions, identity, timestamp, metadata preservation, or missing update. Seven cases cover both limits. |
| `Approve_OverLimit_PreservesEveryFieldWithoutWrite` | Over-limit acceptance, wrong error, pre-validation mutation, or unintended repository update. Full serialized snapshots include every current invoice property. |
| `Approve_UnknownRole_PreservesEveryFieldWithoutWrite` | Unknown/default or out-of-range enum permission; full snapshot and zero writes. |
| `Approve_NonPending_PreservesEveryFieldWithoutWrite` | Draft/Approved/Rejected approvals; full snapshot, existing metadata, and zero writes. |
| `Approve_MissingInvoice_DoesNotChangeOtherInvoicesOrWrite` | Missing-ID handling or unrelated mutation; complete repository snapshot and zero updates. |
| `Approve_WithoutValidSession_ReturnsUnauthorizedAndPreservesInvoice` | Missing/invalid bearer token accepted; unchanged persisted HTTP representation. |
| `Approve_DisallowedSessionRole_ReturnsForbiddenAndPreservesInvoice` | Admin, legacy Approver, or unknown session role accepted; clear error and unchanged state. |
| `Approve_PermittedAccount_UsesSessionIdentityDespiteForgedBody` | Body-supplied identity overriding session identity; configured Manager and both Directors tested. |
| `Approve_OverLimit_IgnoresForgedDirectorAndPreservesInvoice` | Forged Director role bypassing Manager limit or high-value interim rejection; full response snapshot unchanged. |
| `Approve_Draft_ReturnsConflictAndPreservesInvoice` | Incorrect HTTP mapping or draft mutation; error and complete response snapshot asserted. |
| `Approve_MissingInvoice_ReturnsNotFoundWithReason` | Incorrect not-found mapping or unclear reason. |
| `Reject_ExistingAuthorizedAccounts_PreserveRejectionRights` | Lost Manager/Director/Admin rejection rights; verifies status, session identity, and reason. |
| `Delete_RemainsAdminOnly` | Lost Admin delete permission or expanded Manager/Director permission; verifies deletion or unchanged invoice. |

`FakeInvoiceRepository` preserves real mutable-reference behavior, making snapshot checks capable of catching changes made before `Update`. Its update counter independently catches unintended update calls.

Findings

- **TR-S1-01 — P3, optional test improvement:** `Reject_RecordsReasonAndApprover` does not assert `DecidedBy`, despite its name. Add that assertion when strengthening lifecycle tests. Existing API coverage verifies rejection identity, so this does not block stage one.
- **No stage-one correctness or mandatory coverage blockers found.** Domain rules enforce both inclusive thresholds before assignment, and API authorization derives authority from server sessions.
- Dual approvals, duplicate identities, approval-history preservation, and concurrent transitions are deliberately absent in stage one. Their absence is **stage-two work**, consistent with the design’s explicit rejection of all invoices above 500,000 during this intermediate stage.

Pending decisions

Jagdeep’s stage-one review checkpoint. Stage two should remain unimplemented until that review occurs.

## Implementation-agent verdict

TR-S1-01: **fixed**. Added the missing `DecidedBy` assertion to `Reject_RecordsReasonAndApprover`; this directly catches recording the wrong rejection identity. This is an agent decision, not a human acceptance.

All 20 test methods above were reviewed, including every added/changed stage-one method. Parameterized cases yield 45 tests. Browser, mutation, duplicate-user, and concurrency verification remain pending their planned stages.

## Independent stage-two review

The original report below describes the 72-case version reviewed before the optional collection-isolation test was added. Final implementation verification passed 73 cases.

Objective

Independently review stage-two behavior, tests, synchronization, history integrity, and UI changes against the project instructions and OpenSpec.

Changes

None. Read-only review.

Verification

Ran:

`/Users/jagcoool/.dotnet/dotnet test backend/InvoiceApp.sln --no-restore --verbosity quiet`

**72 passed, 0 failed, 0 skipped.**

Reviewed every added or changed test method:

| Method | Defect caught / assertion assessment |
|---|---|
| `Approve_AllowedBoundaries_RecordAuthenticatedDecision` | Existing inclusive limits remain intact; now verifies exactly one history entry, session identity, role, UTC timestamp, and final-decision timestamp equality. |
| `Approve_OverLimit_PreservesEveryFieldWithoutWrite` | Manager rejection still preserves every serialized field and performs no repository write; obsolete high-value interim rejection cases correctly removed. |
| `Reject_RecordsReasonAndApprover` | Prior TR-S1-01 addressed: identity now asserted. |
| `Approve_FirstHighValueApproval_RecordsHistoryAndStaysPending` | Both Manager and Director first approvals remain pending, leave final decision unset, record accurate UTC history, and persist exactly once. |
| `Approve_SecondDistinctDirector_CompletesHighValueInvoice` | Both first-role sequences complete with a distinct Director; first entry preserved, two writes, accurate final identity/time, and earlier detached result unchanged. |
| `Approve_InvalidSecondAttempt_PreservesFullHistoryWithoutWrite` | Duplicate user, second Manager, unknown role, and null/blank identity fail with specific reasons, complete unchanged snapshots, and unchanged write count. |
| `Approve_NonPendingWithHistory_PreservesHistoryAndDecisionMetadata` | Draft/Approved/Rejected invoices with history retain all state and receive no write. |
| `Approve_ThirdApproval_PreservesCompletedInvoiceWithoutWrite` | No third approval after completion; full persisted snapshot and write count preserved. |
| `Reject_AfterFirstApproval_PreservesHistoryAndRecordsFinalRejection` | Rejection after one approval keeps history and records final rejection identity/reason/time. |
| `Reject_EmptyReasonAfterFirstApproval_PreservesHistoryWithoutWrite` | Blank rejection reason preserves history and every other field with no additional write. |
| `Approve_OverLimit_IgnoresForgedDirectorAndPreservesInvoice` | Original API limit rejection remains valid; obsolete high-value interim cases removed. |
| `Approve_HighValueSequence_UsesSessionHistoryAndDistinctDirector` | Real authenticated Manager/Director first sequences, forged bodies ignored, Director second, UTC JSON timestamps, preserved first entry, final response equals stored state. |
| `Approve_SameDirectorInDifferentSessions_IsDeniedWithoutAnyStateChange` | Different tokens do not make one username two approvers; forged second identity ignored, complete HTTP snapshot unchanged. |
| `Approve_UnauthorizedSecondRole_PreservesFullApprovalHistory` | Manager and Admin second attempts fail with appropriate HTTP statuses/reasons and preserve complete history/state. |
| `Approve_ConcurrentDuplicateHttpRequests_OnlyOneHistoryEntrySurvives` | Twelve HTTP requests from one Director yield exactly one success and eleven conflicts, with one entry and no final decision. |
| `Transition_ConcurrentDuplicateDirectors_RecordExactlyOneApproval` | Contending service instances cannot count one Director repeatedly. |
| `Transition_ConcurrentDistinctDirectors_RecordExactlyTwoAndApprove` | Sixteen distinct contending Directors produce exactly two successful approvals and consistent final metadata. |
| `Transition_ApprovalRacingRejection_HasOneConsistentFinalDecision` | Competing terminal decisions cannot overwrite each other; exactly one succeeds and final history/identity/reason match the winner. |
| `Transition_ApprovalRacingDelete_CannotResurrectInvoice` | Delete and approval serialize; removed invoice cannot be recreated by a stale approval transition. |
| `Transition_ReadsDuringApprovals_SeeOnlyCompleteSnapshots` | Readers see pending states with fewer than two entries or fully completed approval states; no partial final metadata. |
| `Snapshots_AddReadListAndTransition_DoNotLeakMutableStoredState` | Mutations through supplied, returned, and listed invoice envelopes do not change stored amount/status/history. |
| `Transition_FailedCallback_DiscardsEvenPartialWorkingCopyChanges` | A callback that mutates then throws cannot leak partial changes. |
| `GetById_LegacyApprovedSeed_KeepsDecisionWithoutInventingHistory` | Existing approved seed retains original metadata and empty history; cannot be reapproved. |

All unchanged stage-one methods were also reread: submission checks; ordinary approval metadata, draft and missing-ID errors; unknown-role and nonpending snapshots; missing-invoice write checks; session authorization; body-forgery protections; Admin rejection and delete permissions. Their assertions remain compatible with detached repository snapshots.

Synchronization assessment:

- `InMemoryInvoiceRepository` is a singleton; `ApprovalService` is scoped. Synchronization therefore correctly belongs to the shared repository.
- `Transition` locks lookup, isolated working-copy creation, full domain validation/mutation, and commit. Failed callbacks never replace stored state.
- `Remove`, `Add`, and all read methods use that same lock, preventing approval/delete resurrection and observable intermediate mutations.
- Snapshot copies isolate mutable invoice envelopes and history collections. History entries are immutable sealed records.
- Username equality is ordinal and consistent with the current exact-match configured-user login. Multiple sessions for the same configured username retain the same approval identity.
- UI history renders through text content; first-approval guidance, final progress, and legacy history handling match the implemented behavior.

Findings

**TR-S2-01 — P3, optional test-strengthening gap:**
`Snapshots_AddReadListAndTransition_DoNotLeakMutableStoredState` tests replacing the `Approvals` property, but not mutation of an originally supplied nonempty history array. Extend it by adding an invoice with an `ApprovalEntry[]`, changing that source array after `Add`, and asserting persisted history remains unchanged. Also consider asserting returned history rejects indexed mutation via `IList<ApprovalEntry>`. This would protect the collection-copy/read-only behavior against future regressions. **Current production code correctly copies and wraps these collections; this is not a correctness blocker.**

No stage-two correctness blockers found. TR-S1-01 is fixed.

Pending decisions

Agent recommendation: stage two is ready for its commit and Jagdeep’s review checkpoint. The optional P3 test improvement can be addressed now or recorded with a reason.

Playwright, mutation testing, CI, workshop review, and PR validation were outside this review and were not run.

## Stage-two implementation-agent verdicts

- TR-S2-01: **fixed**. Added `Snapshots_HistoryArraysAndCollectionCasts_CannotMutateStoredEntries`, proving caller-supplied nonempty arrays cannot alter stored history and returned collection setter/Clear throw `NotSupportedException`. Full snapshot equality is asserted.
- Main-agent test review: **fixed**. Invalid-second-attempt cases now assert exact `ArgumentException` or `InvalidOperationException` instead of any exception.
- Main-agent concurrency review: **fixed**. Dedicated workers signal readiness before common release; readiness and completion are bounded to ten seconds.
- Main-agent live UI findings: **fixed**. Hidden session bar overrides flex display; decision errors clear across account changes.

These are agent verdicts. Jagdeep’s stage-two acceptance remains pending.

## Independent stage-two addendum

Objective

Verify the stage-two review follow-up.

Changes

None; read-only addendum.

Verification

Inspected `Snapshots_HistoryArraysAndCollectionCasts_CannotMutateStoredEntries`: it changes the supplied array, verifies indexed assignment and `Clear` through `IList<ApprovalEntry>` throw `NotSupportedException`, and checks the stored entry and full snapshot remain unchanged.

Confirmed invalid-second-approval cases now assert exact exception types. Confirmed concurrency workers signal readiness before release, with bounded readiness and completion waits.

Reran `/Users/jagcoool/.dotnet/dotnet test backend/InvoiceApp.sln --no-restore --verbosity quiet`: **73 passed, 0 failed, 0 skipped**.

Findings

**TR-S2-01: resolved.** No new findings.

Pending decisions

Stage-two commit and Jagdeep’s review checkpoint remain pending.

## Main-agent live UI verification

The main agent ran the application locally and operated Chrome through the UI (not an automated Playwright test):

- Created/submitted 500,000.01; Manager first approval stayed pending with one history entry. The same Manager's second attempt showed the duplicate-user error and retained the same first history entry/time. A different Director completed it with two recorded approvals.
- Created/submitted 700,000; Director first approval stayed pending. The same Director was denied a second approval. After signing in as Manager, the previous user's error was cleared; attempting second approval showed the Director-required error. A different Director then completed it.
- Verified history and counts on the approved-invoice table, including existing legacy approved seed handling.
- Observed and fixed anonymous Sign out visibility and stale errors after changing users. Rechecked both with current UI assets in a fresh local browser origin.

Main verification: `dotnet test backend/InvoiceApp.sln --no-restore` — **73 passed, 0 failed, 0 skipped**; OpenSpec strict validation, 5 logger checks, JavaScript syntax and whitespace checks passed. The required automated Playwright scenario and mutation measurement remain later verification work.

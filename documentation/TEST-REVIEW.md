# Test review

Baseline: seven existing xUnit tests passed before implementation.

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

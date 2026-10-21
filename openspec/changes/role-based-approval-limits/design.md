# Design

## Context

See proposal.md for motivation. Existing Domain ApprovalService mutates repository-returned invoice references; Api scopes one service per request over a singleton repository. The UI already renders server errors. No durable database exists.

## Goals / Non-Goals

**Goals:** enforce the specification in Domain; trust server-session credentials; keep failures atomic and observable; produce reproducible evidence.

**Non-Goals:** production authentication, database persistence, framework replacement, bulk approval, or partial monetary approvals.

## Decisions

- Introduce a typed domain approval role; map session role strings in Api. No caller-supplied user or role is trusted. Retain the existing approve route and response/error conventions.
- Stage one changes Approve to accept role and rejects all amounts above 500,000 with an explicit dual-approval-required reason. Stage two later enables first/second approvals. This safe intermediate behavior never prematurely approves a high-value invoice.
- Store immutable approval entries (username, role, UTC timestamp) on invoices in stage two. Preserve DecidedBy and DecidedAt for the final approval only; the first approval leaves status pending. Existing approved seed invoices remain valid legacy records.
- In stage two introduce shared repository transaction/synchronization semantics across service instances. Locking a request-scoped service is insufficient. Cover the full validation/read-modify-write transition and competing lifecycle decisions, avoiding mutation until every check passes. Use canonical authenticated usernames and compare identities consistently.
- Manager and Director can approve or reject; Admin retains existing rejection and destructive administration capability but cannot approve. Use action-level authorization to preserve rejection behavior while changing approval authority.
- Keep the existing UI and accessible controls; add progress/history without disabling the rejected-approval demo path. Domain errors remain the authoritative enforcement.
- Use full snapshots plus write-count assertions for rejection tests; API integration tests authenticate against real middleware. Browser tests create unique invoices rather than relying on seed order.
- Pin OpenSpec and Stryker tooling. Measure mutation scores before/after against unchanged production code and identical approval-service scope. Do not weaken tests to manufacture a gain.

## Risks / Trade-offs

- In-memory data resets on restart -> document and use isolated test hosts.
- Mutable references can leak changes before Update -> validate before assignment and assert full-state equality.
- Existing Approver clients lose approval authority -> document demo account migration to Manager; unknown roles denied.
- Native hooks require explicit trust and may not cover this already-running chat -> smoke-test actual hooks separately, record this session's orchestration explicitly, and disclose provenance.
- Workshop review prompt/video unavailable -> mark pending and do not claim completion.

## Migration Plan

Keep main at upstream baseline and deliver on a feature branch in the public fork. No persistent data migration. Run unit/API/browser checks before review. Rollback is reverting the feature branch commits; demo data is recreated on restart.

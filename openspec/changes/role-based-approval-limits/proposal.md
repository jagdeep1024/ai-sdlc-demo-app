# Proposal

## Why

The current service approves any pending invoice in one step without role or amount checks. The assignment requires enforced approval limits and evidence that rejection cannot mutate an invoice or count one user twice.

## What Changes

- Enforce Manager 50,000 and Director 500,000 inclusive limits.
- Require two distinct approvers above 500,000; the second must be a Director.
- Persist approval history and show progress in the existing web UI.
- Reject invalid attempts with useful errors and no invoice changes.
- **BREAKING**: replace the demo Approver role with Manager; Admin no longer approves.
- Add boundary, state-invariance, API, concurrency and browser tests with mutation evidence.

## Capabilities

### New Capabilities

- `invoice-approval`: role limits, dual authorization, immutable failures and visible approval progress.

### Modified Capabilities

None; no previous OpenSpec capabilities exist.

## Impact

Domain approval service and invoice model; API session integration, roles and demo users; static UI; xUnit and Playwright tests. Approval URL stays unchanged; invoice responses gain approval history. No database, framework migration, or new business feature.

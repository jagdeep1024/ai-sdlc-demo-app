# Invoice Approval Demo App

A small invoice approval app used for live AI-in-SDLC demos. It is deliberately simple: a .NET 8 API, a plain web UI, and an in-memory store, so it runs with one command and no database.

[Assignment write-up and submission status](ASSIGNMENT.md) · [PR #1](https://github.com/jagdeep1024/ai-sdlc-demo-app/pull/1) · [Review findings and verdicts](documentation/REVIEW-FINDINGS.md)

## Run it

```
dotnet run --project backend/src/InvoiceApp.Api
```

Open http://localhost:5000 and sign in:

| User | Password | Role |
| --- | --- | --- |
| `demo.approver` | `ChangeMe123!` | Manager |
| `demo.director` | `ChangeMe123!` | Director |
| `demo.director2` | `ChangeMe123!` | Director |
| `demo.admin` | `ChangeMe123!` | Admin |

These are demo accounts for a local, in-memory app. They are not real credentials.

## What it does

1. Create an invoice (it starts as a draft).
2. Submit it for approval.
3. An approver approves it, or rejects it with a reason.

Invoices move through Draft, Pending Approval, then Approved or Rejected.

## Tests

```
dotnet test backend/InvoiceApp.sln
```

Browser rejection test (fresh app instance on port 5097):

```sh
cd e2e
npm ci
npx playwright install chromium
npm run typecheck
npm test
```

Mutation testing from the repository root:

```sh
dotnet tool restore
cd backend/tests/InvoiceApp.Tests
dotnet stryker --skip-version-check
```

Stryker is pinned to 4.13.0 for .NET 8. Approval-service mutation coverage improved from **89.09% to 94.55%** with unchanged production code and mutation settings. See [measurement scope, counts and remaining gaps](documentation/MUTATION-RESULTS.md) and [browser-test evidence](documentation/BROWSER-TEST-REVIEW.md).

GitHub Actions runs specification, logger, unit/API, TypeScript, Chromium, and mutation checks on feature-branch pushes and PRs into main. Browser and mutation reports are uploaded as run artifacts. The mutation minimum is 80%; see the [CI workflow](.github/workflows/ci.yml) for exact commands.

## Project layout

```
backend/
  src/InvoiceApp.Domain   business rules
  src/InvoiceApp.Api      controllers, sign-in, web UI (wwwroot)
  tests/InvoiceApp.Tests  xUnit tests
docs/use-cases            short feature briefs
AGENTS.md                 conventions for AI coding agents
```

## Use cases

- [UC-1 Bulk invoice approval](docs/use-cases/UC-1-bulk-approval.md)
- [UC-2 Partial approval](docs/use-cases/UC-2-partial-approval.md)

## Approval limits

| Amount | Required approval |
| --- | --- |
| Up to 50,000 inclusive | Manager or Director |
| Above 50,000 through 500,000 inclusive | Director |
| Above 500,000 | Manager or Director first, a different Director second |

The first high-value approval leaves the invoice pending with final decision fields unset. A second approval from a different Director completes it. The same authenticated username can never count twice, including after signing in again. Usernames are the canonical case-sensitive identities used by demo sign-in.

Each successful approval records immutable username, role, and UTC timestamp entries. The UI shows progress and history for pending and approved invoices. Existing approved seed invoices retain their legacy decision fields without invented approval records.

All failed approvals preserve every invoice field and perform no repository write. Repository-owned atomic transitions serialize lifecycle decisions and deletion across requests; stored data and response snapshots share no mutable invoice or history collections.

Approval identity and role come only from the authenticated server session. Admin can reject and delete invoices but cannot approve them. The former Approver demo account is now Manager. The first high-value approver role is an explicit assignment assumption; no Chairman role is introduced.

The app retains USD display and decimal amounts. This is an in-memory demo: data and sessions reset on restart. No persistent migration is required.

# Project conventions for AI agents

Invoice approval demo app: ASP.NET Core 8 Web API with a small static web UI.

## Layout

- `backend/src/InvoiceApp.Domain`: business rules, no web or storage code. `ApprovalService` owns the invoice lifecycle (Draft, PendingApproval, Approved, Rejected).
- `backend/src/InvoiceApp.Api`: controllers, sign-in (`Auth/`), in-memory repository, and the web UI under `wwwroot/`.
- `backend/tests/InvoiceApp.Tests`: xUnit tests, one test class per class under test, named `<ClassUnderTest>Tests`.
- `e2e`: Playwright end-to-end tests (`tests/`) and page objects (`pages/`).
- `docs/use-cases`: short briefs for features we may build.

## Conventions

- Controllers live in `Controllers/`, one per resource, route `api/[controller]`. Request bodies are `record` types declared inside the controller.
- Controllers inherit `ApiControllerBase` and use `Handle(...)` so service errors map to 404, 409 and 400.
- Every endpoint is behind sign-in. Restrict actions with `[RequireRole("...")]`: destructive actions are Admin only, approvals are Manager or Director; Admin has no approval authority.
- Business rules go in the Domain project, never in controllers.
- Register services in `Program.cs`. Repositories are interfaces in Domain with an in-memory implementation in Api.
- Every new rule or endpoint needs tests. Use `FakeInvoiceRepository` in tests.
- Use clear names; no one-letter variables or `flag` parameters.
- End-to-end tests use page objects and role-based locators (`getByRole`, `getByLabel`), never CSS or XPath selectors.

## Commands

- Run the app: `dotnet run --project backend/src/InvoiceApp.Api` then open http://localhost:5000
- Run the tests: `dotnet test backend/InvoiceApp.sln`
- Run end-to-end tests: `cd e2e && npm ci && npx playwright install chromium && npm run typecheck && npm test`

- Run mutation testing: `dotnet tool restore` at repository root, then `cd backend/tests/InvoiceApp.Tests && dotnet stryker --skip-version-check`. Keep the recorded source and configuration unchanged for score comparisons.

## Demo accounts (not real credentials)

- `demo.approver` / `ChangeMe123!` (role Manager)
- `demo.director` and `demo.director2` / `ChangeMe123!` (role Director)
- `demo.admin` / `ChangeMe123!` (role Admin)

## Never

- Commit real credentials, tokens or customer data.
- Add business logic to `Program.cs` or controllers.

## Assignment workflow

Read `.agents/skills/assignment-evidence/SKILL.md` for assignment work.
Use OpenSpec change `role-based-approval-limits` as the behavioral source of truth.
Manager limit: 50,000 inclusive. Director limit: 500,000 inclusive.
Above 500,000: Manager or Director first, then a distinct Director. Same identity never counts twice.
Failed attempts return a clear reason and leave ALL invoice fields and repository writes unchanged.
Session identity/role are authoritative; never accept them from an approval request body.
Keep rules in Domain, transport/auth in Api, presentation in wwwroot.
Implement single-approver limits with tests in one stage and commit, then dual approval in a separate prompt/stage/commit.
Do not implement stage two until Jagdeep reviews stage one.
Main agent coordinates; implementation agent may delegate an independent read-only test review.
Review every generated test for a real failing scenario, not just execution coverage.
Review findings require stable IDs, evidence, and fixed/dismissed/deferred verdicts with reasons.
Record actual AI interactions and distinguish human instructions from delegated prompts; never fabricate human decisions.
The workshop-specific review prompt was unavailable; Jagdeep authorized the standard fallback recorded in documentation/REVIEW-PROMPT.md. The final video is pending.

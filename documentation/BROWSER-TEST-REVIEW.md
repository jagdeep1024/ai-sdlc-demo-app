# Browser test and CI verification

## Rejected over-limit approval

`e2e/tests/approval-limits.spec.ts` contains one Chromium end-to-end test using `InvoiceAppPage` and accessible role/label locators. It uses the real application without request mocks.

| Assertion | Mistake it catches |
| --- | --- |
| Manager identity visible after UI sign-in | Testing the wrong role or bypassing sign-in |
| UI-created unique customer and amount 50,000.01 | Seed dependence or accidentally testing an allowed amount |
| Submitted response is pending with empty history and unset decision fields | An invalid initial state that could make a rejection test pass for the wrong reason |
| Approve UI request receives 409 | Accidental approval or wrong API error mapping |
| Visible alert contains the exact Manager limit and required Director reason | An absent, hidden, or generic error message |
| Complete invoice after reload equals the pre-attempt invoice | Changes to amount, status, history, identity, timestamps, notes, or other returned fields after rejection |
| Same invoice visibly remains pending with zero recorded approvals | Stale UI state or missing invoice after reload |
| Same invoice still has an approval control in the pending tab | Invoice disappearing from the pending workflow |

The test observes the real submit response and the UI's authenticated refresh response after reload. It does not read tokens or browser storage. Page-object waits are registered before the triggering clicks or reload.

## Reproducible commands

Requires the .NET SDK selected by root `global.json` and Node.js 24 in PATH.

```sh
cd e2e
npm ci
npx playwright install chromium
npm run typecheck
npm test
```

The runner starts its own app at `http://127.0.0.1:5097` with `--no-launch-profile` and `reuseExistingServer: false`. An occupied port fails the run rather than reusing another app. Browser dependency versions are pinned in the manifest and lockfile. HTML reports, screenshots, and failure traces are generated outside tracked source.

## Local results

- TypeScript: passed.
- Chromium: **1 passed**, complete run approximately 5 seconds.
- Initial setup attempt began before Chromium finished downloading and failed because its executable was absent. After installation completed, the real UI test passed.
- Git whitespace check: passed.

## CI configuration

The workflow runs for feature-branch pushes, pull requests targeting main, and manual dispatch. It grants only `contents: read`, selects .NET through `global.json`, uses Node.js 24, caches both npm lockfiles, and runs specification, logger, domain/API, TypeScript, and Chromium checks. Browser reports and failure diagnostics are uploaded for completed runs, including failures.

At the pre-push review below, hosted execution was still pending. The workflow now also runs a separate pinned Stryker job with an 80% minimum and uploads its report. Actual hosted results are available in the repository’s GitHub Actions checks; local results alone do not establish hosted success. Mutation measurements are recorded in MUTATION-RESULTS.md.

## Independent review

Objective

Independently review the submitted browser-test path and CI configuration without changing files or rerunning Chromium.

Changes

None.

Verification

Read the page object, browser test, Playwright/TypeScript configuration, package manifest and lockfile, CI workflow, root tooling configuration, and UI handlers.

Ran `npm run typecheck --prefix e2e`: **passed**.

Helper and assertion review:

| Helper / assertion | Assessment |
|---|---|
| `signInAsManager` | Uses real UI credentials, labels, and button; verifies authenticated app and Manager identity. |
| `createInvoice` | Creates through UI; response waiter registered before clicking; verifies POST endpoint, HTTP 201, and matching invoice row. |
| `submitInvoice` | Submits through accessible invoice-specific button; waits for exact POST route, verifies HTTP 200 and visible pending state. |
| `openApprovals` | Uses the named accessible tab. |
| `approveInvoice` | Clicks actual invoice-specific approval control; pre-registers exact POST response waiter. |
| `decisionAlert` | Scopes the alert to the Approvals tab panel. |
| `invoiceRow` | Uses accessible row/cell locators and exact invoice ID, avoiding seed ordering or customer-name ambiguity. |
| `reloadAndReadInvoice` | Observes the UI’s actual authenticated GET after reload; checks HTTP 200, same invoice presence, and visible row. No browser-storage inspection or API shortcut. |
| Unique customer UUID | Prevents accidental matching of unrelated test data. |
| Initial snapshot assertions | Establish amount `50000.01`, pending status, empty history, unset decision metadata, and exact created identity/customer. |
| HTTP 409 assertion | Catches accidental success or incorrect rejection mapping. |
| Exact visible error assertion | Confirms the UI displays the specific Manager-limit reason. |
| Full `after.toEqual(before)` | Protects every server-returned field, including creation time, history, and decision metadata. |
| Pending cell after reload | Independently verifies visible persisted lifecycle state. |
| `0 of 1 approvals recorded` | Verifies visible zero-history progress after rejection and reload. |
| Approval button remains visible | Confirms the same invoice remains available in pending approvals. |

The test performs real UI login, creation, submission, and approval. It uses no mocks, direct state injection, hidden browser storage, or API-created test invoices.

Configuration assessment:

- Fresh server at explicit `127.0.0.1:5097`; `--no-launch-profile` avoids launch-profile port overrides.
- `reuseExistingServer: false` prevents testing a stale instance.
- Plain `dotnet` command uses PATH without machine-specific executable paths.
- Single Chromium worker, CI retry, failure traces/screenshots, and HTML report configured.
- Strict TypeScript includes config, page objects, and tests.
- Exact direct dependency versions agree with the committed lockfile.

CI assessment:

- Triggers on feature/main pushes, PRs targeting main, and manual dispatch.
- Read-only repository permissions.
- Uses `global.json` for .NET SDK and Node 24 with both npm lockfiles cached.
- Runs reproducible installs, strict OpenSpec validation, evidence-logger tests, domain/API tests, TypeScript checking, Chromium installation, and browser tests.
- Uploads browser report and failure diagnostics after successful or failed noncancelled runs.
- Mutation-job additions remain separately owned by the main agent.

Findings

**No actionable browser-test or reviewed CI defects found.** No new finding IDs required.

Pending decisions

Browser execution was not repeated during this review to avoid interfering with the active mutation run. Its reported prior 1/1 result is implementation evidence, not a new execution by this reviewer. Remote GitHub Actions execution remains to be verified after pushing.


Implementation-agent verdict: accepted the review conclusion; no changes required. This is an agent decision. Hosted CI verification remains pending.

## First hosted run and portability fix

[Run 39637162288](https://github.com/jagdeep1024/ai-sdlc-demo-app/actions/runs/39637162288) ran after enabling workflows on the fork. The mutation job passed. The verification job stopped at the logger test, before browser execution.

**CI-01 — fixed (agent verdict):** the logger test iterated all dotted files, including its empty lockfile, and checked the final iterated file for a redaction marker. Linux directory order exposed this latent test defect. It now checks both named published outputs explicitly, retains every forbidden-secret assertion, and checks redaction in each output. Missing outputs fail directly. Independent review confirmed the assertions were strengthened and all five logger checks passed locally. The logger implementation did not change.

This fix requires a new hosted run; the first failed run is retained as evidence rather than described as successful.

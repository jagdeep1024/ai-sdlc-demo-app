# Mutation testing results

Measured on 21 October 2026 using **dotnet-stryker 4.13.0** on .NET SDK 8.0.426. These are actual completed measurements, not estimated scores.

```text
Before: 89.09% = 49 killed / 55 scored mutants
        4 survived, 2 no coverage, 6 compile errors, 38 ignored
        40 selected ApprovalServiceTests cases
After:  94.55% = 52 killed / 55 scored mutants
        1 survived, 2 no coverage, 6 compile errors, 38 ignored
        49 selected ApprovalServiceTests cases
Change: +5.46 percentage points; 3 additional mutations detected
```

Both runs generated 99 mutants overall: 75 in ApprovalService and 24 outside the selected file scope. The ignored count includes 24 excluded by the file filter and 14 automatically ignored by Stryker's block-already-covered filter. Compile errors and ignored mutants do not enter the score; no-coverage mutants **do** count as undetected. No mutants timed out.

## Comparison integrity and scope

- Baseline tests: commit `1f457d9db420101d3e4ca46ec9ad5fa109a85e4c` before the new mutation-gap tests.
- After tests: that same baseline plus the three methods in `ApprovalServiceTests.MutationGaps.cs` (nine parameterized cases).
- All 17 production C# file hashes and the Stryker configuration hash were verified identical between runs. The approval-service SHA-256 is `e5b96bde62d701461fbc7a7f856d0a934f590c5bc97d5b2b31ca7a8b52494bbb`.
- Scope is **InvoiceApp.Domain/ApprovalService.cs**, Standard mutation level, running `ApprovalServiceTests`. The score is not a whole-application score: repository synchronization, API authorization, UI and unrelated DiscountCalculator code are outside this mutation scope and have separate tests.
- No production edits, new mutation exclusions, manual ignored mutants, weakened tests, or changes in threshold were used between measurements.
- The configured CI minimum is 80%; both measurements used this same threshold.

Full tool-generated JSON reports are retained as [baseline.json](mutation/baseline.json) and [final.json](mutation/final.json), with machine-specific repository path prefixes normalized. [comparison.json](mutation/comparison.json) records counts, source hashes, configuration hash, scope, and remaining mutant IDs. No mutation statuses or test outcomes were altered during normalization.

## Survivor-driven improvements

| Baseline ID | Missing assertion / behavior | Test improvement | Actual final status |
| --- | --- | --- | --- |
| 4 | Empty non-Draft submission error still passed | All non-Draft states must explain failure and preserve the full invoice with zero writes | Killed |
| 67 | Empty missing-rejection-reason message still passed | Null/empty/whitespace reason after a real first approval must give a clear reason and preserve all history/fields | Killed |
| 69 | Removing Reject's pending-state check still passed | Rejecting Draft/Approved/Rejected states must not overwrite the original lifecycle decision; completed states are created through real transitions | Killed |

All three generated test methods were independently reviewed before publication. The complete suite after the improvements passed **82 unit/API/concurrency cases**; the mutation-filtered suite includes 49 of them.

## Remaining undetected mutations

| ID | Status | Behavior change |
| --- | --- | --- |
| 29 | Survived | Changes the defensive approval-count check from `>= 2` to `> 2` |
| 32 | No coverage | Removes the throw for an already-two-approvals pending invoice |
| 33 | No coverage | Empties that defensive error reason |

These affect an inconsistent stored state: PendingApproval with two recorded approvals. The supported API completes the second approval atomically by setting Approved; further attempts fail the earlier pending-state check. Normal API transitions therefore do not exercise this defensive branch.

They are **not proven equivalent mutants**: manually seeding an inconsistent invoice through the domain/repository interface can make them observable. They remain in the denominator and are disclosed as residual defensive-state coverage gaps. No artificial invalid-state tests or new exclusions were added solely to obtain 100%.

## Reproduce

With .NET and Node prerequisites installed, from the repository root:

```sh
dotnet tool restore
cd backend/tests/InvoiceApp.Tests
dotnet stryker --skip-version-check
```

The committed config selects the same file, test filter, mutation level, concurrency and thresholds. It emits HTML and JSON reports under ignored `StrykerOutput/`. The measurements above used the same command with separate output directories; that output-location difference does not change mutation selection or scoring.

To reproduce the before measurement, use baseline commit `1f457d9` with the same committed Stryker configuration; it predates only the new test file. Keep both production code and configuration unchanged when comparing. Hosted CI runs the final measurement and uploads the generated report; its actual check result is available in GitHub Actions.

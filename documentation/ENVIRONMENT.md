# Environment and baseline

- Public fork: https://github.com/jagdeep1024/ai-sdlc-demo-app
- Upstream baseline: `66cb3f9005af1feafb7890f06bbc41fd652b432a`.
- Feature branch: `codex/role-based-approval-limits`; fork main stays at baseline.
- Local .NET SDK: 8.0.426, installed from Microsoft's dotnet-install script under `$HOME/.dotnet`.
- Node.js: 24.18.0. OpenSpec: 1.14.3 (root npm lockfile).
- Stryker: 4.13.0 (local .NET tool manifest; .NET 8 compatible).
- Baseline command: `dotnet test backend/InvoiceApp.sln`.
- Baseline result: 7 passed, 0 failed, 0 skipped, before production changes.

If `dotnet` is not on PATH: `export PATH="$HOME/.dotnet:$PATH"`.
Restore tool dependencies with `npm ci` and `dotnet tool restore`.

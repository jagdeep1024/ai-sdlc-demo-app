# Evidence workflow

`documentation/ai-interactions.jsonl` is the append-only source. `AI-INTERACTION-LOG.md` is a deterministic rendering in append order. Each record identifies the source, speaker, event, UTC capture time and payload. A retrospective entry must say it is retrospective; its capture time is not the original interaction time. An agent decision is not a human decision.

## Record an actual interaction

Pass a JSON object on standard input to `python3 scripts/ai_log.py append`. Required keys: `source`, `speaker`, `event`, `data`. An optional stable `id` makes retries idempotent; reuse with different content fails. Use identifier strings for the first three fields; put free text in `data`. For example, after an actual delegated task, record its prompt with source `agent-delegation`, speaker `main-agent`, event `prompt`, and `data` containing the exact prompt and target. Record the returned report as another event, speaker identifying the worker. Never record a draft prompt as executed.

Render again with `python3 scripts/ai_log.py render`. Run logger checks with `python3 -m unittest discover -s scripts/tests -v`.

## Project hooks

`.codex/hooks.json` defines command handlers for `UserPromptSubmit`, `Stop`, `SubagentStart`, and `SubagentStop`, following the [official Codex hooks documentation](https://learn.chatgpt.com/docs/hooks), checked on 21 October 2026. The same Python entry point selects documented fields, appends them, and returns the empty JSON object required by completion hooks. No hook asks the agent to continue or blocks a prompt deliberately.

Review both the definitions and Python script. Project-local `.codex` configuration must be trusted; use Codex `/hooks` to review and trust the exact definitions. Changed definitions require renewed trust. Open the assignment repository as the working project and start a session that loads its project hooks. Do not bypass trust. Confirm a real prompt and its completion produce matching `codex-hook` source records before claiming automatic capture. Directly invoking the handler with test input checks its contract, not runtime activation.

This desktop conversation began outside the fork and predates these files. Hook registration, trust, and delivery in this already-running conversation have not been verified. Its actual coordination is recorded through explicit CLI calls. No claim of complete automatic historical capture is made. The configuration supports future project sessions once loaded and trusted.

`SubagentStart` provides identifiers and type but does not include the delegated prompt. Record delegated prompts explicitly through the CLI. Hook outputs include only the latest assistant message, not every intermediate tool call or response. `UserPromptSubmit` is labelled `prompt-submitter`, not automatically asserted to be a human. Use explicit source/speaker metadata for human instructions versus orchestration. Specialized tool paths may not emit hooks. Before submission, reconcile recorded milestones against the actual session and identify any gaps.

## Privacy and integrity

The logger never opens transcript paths or exports other sessions. Hook events whose working directory is outside this repository fail. Source events are redacted before writing: sensitive dictionary keys, authentication headers, recognizable tokens, credential assignments, and the public demo password are removed. Set `AI_LOG_REDACT_VALUES` to a JSON array of additional known secret strings in the environment if necessary; do not put real secrets in commands or committed files. Pattern redaction is not a universal secret detector: review public evidence before publishing and avoid passing secrets initially.

A file lock serializes append and render, append flushes are synced, and readable output is replaced atomically. Duplicate explicit event IDs are safe retries; hook IDs derive from the event identity and selected payload. The logger never silently rewrites existing source entries. A process crash after appending can leave the readable view stale; `render` recovers it. If a secret is discovered in committed evidence, stop publication and address it explicitly rather than claiming append-only integrity prevents redaction.

The logger uses Python's POSIX `fcntl` on macOS/Linux. Windows users need WSL. Ignore `documentation/.ai-log.lock` and Python `__pycache__` folders; these are runtime artifacts.

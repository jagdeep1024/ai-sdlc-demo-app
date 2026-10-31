# Evidence workflow

`documentation/ai-interactions.jsonl` is the structured augmentation source. Runtime recording is append-only; the explicit schema migration described below preserves each original event. `AI-INTERACTION-LOG.md` is a deterministic rendering in append order. Each record identifies the source, speaker, event, UTC capture time and payload. A retrospective entry must say it is retrospective; its capture time is not the original interaction time. An agent decision is not a human decision.

## Record an actual interaction

Pass a JSON object on standard input to `python3 scripts/ai_log.py append`. Required keys: `source`, `speaker`, `event`, `data`. An optional stable `id` makes retries idempotent; reuse with different content fails. Use identifier strings for the first three fields; put free text in `data`. For example, after an actual delegated task, record its prompt with source `agent-delegation`, speaker `main-agent`, event `prompt`, and `data` containing the exact prompt and target. Record the returned report as another event, speaker identifying the worker. Never record a draft prompt as executed.

Render again with `python3 scripts/ai_log.py render`. Run logger checks with `python3 -m unittest discover -s scripts/tests -v`.

## Reviewer-aligned augmentation format

I adopted the augmentation-log fields shared in the workshop chat on 27 October 2026 and supplied with my documentation revision request on 31 October. The workshop allows a custom schema; my log uses its suggested field names and keeps the original evidence envelope as additional fields.

Each JSONL record now contains `id`, `timestamp`, `stage`, `agent`, `agent_name`, `agent_type`, `ai_tool`, `model`, `mode`, `related_ids`, `parent_task`, `context`, `request`, `skills_used`, `subagents_used`, `mcp_tools_used`, `tools_used`, `suggestion`, `human_decision`, `action_verification`, `limitation`, `status`, and `artifacts`. The machine-readable shape is in [augmentation-log.schema.json](augmentation-log.schema.json).

- Original `source`, `speaker`, `event`, and `data` are retained. `data` includes original reports, outputs, corrections, execution evidence and commit/session references; these are not replaced with reconstructed summaries.
- `model` is populated when the original hook payload actually recorded it. It is not inferred from another session or today's configuration. `mode` remains null unless supplied explicitly.
- Nulls and empty arrays in historical entries mean **not separately recorded**, not that a tool, skill or integration was unused. The original payload may contain more detail. No tool usage or human approval is invented to populate the suggested fields.
- `human_decision` is populated from explicitly human-attributed source events. An agent verdict or an agent-authored hook prompt never becomes a human decision.
- A default `status` of `recorded` means the event was captured; it does not assert success. Historical outcomes remain in the original payload. New events can explicitly report `completed`, `partial`, `failed`, or another precise status such as `applied`.
- Historical stages were assigned from the retained sequence of setup, single-approver, dual-approval, browser/mutation/CI, PR-review and video checkpoints. They are organizational labels added during migration, not evidence of new executions.

I requested this format change after the work had already been recorded. The explicit `python3 scripts/ai_log.py migrate` operation adds `schema_version: 2` and normalization provenance while preserving original IDs, capture times, source, speaker, event and payload. It is idempotent. Subsequent recording remains append-only; `render` regenerates the readable summary without rewriting the source. The migration does not claim full historical automatic capture.

For new CLI events, pass reviewer fields alongside the existing `source`, `speaker`, `event`, and `data` keys. Record exact skill names/types/purposes/invocation, delegated tasks and outcomes, tool/server names, affected paths and actual results when known. Do not copy the workshop's placeholder values into real records. Hooks use the same normalization path and retain only the event metadata actually delivered. The original `.codex/hooks.json` commands remain compatible.

## Project hooks

`.codex/hooks.json` defines command handlers for `UserPromptSubmit`, `Stop`, `SubagentStart`, and `SubagentStop`, following the [official Codex hooks documentation](https://learn.chatgpt.com/docs/hooks), checked on 21 October 2026. The same Python entry point selects documented fields, appends them, and returns the empty JSON object required by completion hooks. No hook asks the agent to continue or blocks a prompt deliberately.

Review both the definitions and Python script. Project-local `.codex` configuration must be trusted; use Codex `/hooks` to review and trust the exact definitions. Changed definitions require renewed trust. Open the assignment repository as the working project and start a session that loads its project hooks. Do not bypass trust. Confirm a real prompt and its completion produce matching `codex-hook` source records before claiming automatic capture. Directly invoking the handler with test input checks its contract, not runtime activation.

This desktop session predates these hook definitions. Hook registration, trust, and delivery in this already-running conversation have not been verified. Its actual coordination is recorded through explicit CLI calls. No claim of complete automatic historical capture is made. The configuration supports future project sessions once loaded and trusted.

`SubagentStart` provides identifiers and type but does not include the delegated prompt. Record delegated prompts explicitly through the CLI. Hook outputs include only the latest assistant message, not every intermediate tool call or response. `UserPromptSubmit` is labelled `prompt-submitter`, not automatically asserted to be a human. Use explicit source/speaker metadata for human instructions versus orchestration. Specialized tool paths may not emit hooks. Before submission, reconcile recorded milestones against the actual session and identify any gaps.

## Privacy and integrity

The logger never opens transcript paths or exports other sessions. Hook events whose working directory is outside this repository fail. Source events are redacted before writing: sensitive dictionary keys, authentication headers, recognizable tokens, credential assignments, and the public demo password are removed. Set `AI_LOG_REDACT_VALUES` to a JSON array of additional known secret strings in the environment if necessary; do not put real secrets in commands or committed files. Pattern redaction is not a universal secret detector: review public evidence before publishing and avoid passing secrets initially.

A file lock serializes append and render, append flushes are synced, and readable output is replaced atomically. Duplicate explicit event IDs are safe retries; hook IDs derive from the event identity and selected payload. The logger never silently rewrites existing source entries; the separately requested, explicit schema migration enriches their structure without changing original evidence fields. A process crash after appending can leave the readable view stale; `render` recovers it. If a secret is discovered in committed evidence, stop publication and address it explicitly rather than claiming append-only integrity prevents redaction.

The logger uses Python's POSIX `fcntl` on macOS/Linux. Windows users need WSL. Ignore `documentation/.ai-log.lock` and Python `__pycache__` folders; these are runtime artifacts.

## Verified native smoke test

On 21 October 2026, the coordinating agent reviewed these exact definitions and logger code, used the normal CLI folder trust and `/hooks` review/trust flow, and submitted a no-tools smoke prompt in a separate read-only Codex CLI session. No hook-trust bypass flags were used.

The append-only source contains a matching `codex-hook` UserPromptSubmit and Stop pair for session `01a15f1b-32f8-7283-871b-8fab9f6d3178`, turn `01a15f1b-8b1f-7270-bf80-65314d8b90aa`. The response was `Hook smoke test received.` The prompt itself identifies the coordinating AI as its author. This proves prompt/completion hook delivery in that session only; native subagent event delivery and this desktop chat remain unverified. Explicit delegation capture remains necessary.

## Publication scope

Published summary entries omit non-assignment context. An editorial scope correction was applied to the earlier retrospective planning and setup summaries; actual implementation prompts, test outcomes and review decisions were preserved. Runtime appends remain append-only after redaction.

---
name: assignment-evidence
description: Implement or review this invoice-approval assignment with staged tasks and traceable test and interaction evidence.
---

Read AGENTS.md and the active OpenSpec change before coding or reviewing.
Keep stage one and stage two separate. Stop at the requested human review checkpoints.
For each task, report these exact fields: Objective, Changes, Verification, Findings, Pending decisions.
Include commands and real outcomes under Verification; identify unrun checks.
Log delegated prompts and returned reports through scripts/ai_log.py when available; retain speaker and source identifiers.
Human decisions require an actual human response. Agent review decisions must be labelled as agent decisions.
Test reviews explain the defect each test would catch and inspect full-state equality on rejected attempts.
No secrets or unrelated conversations in submitted logs.

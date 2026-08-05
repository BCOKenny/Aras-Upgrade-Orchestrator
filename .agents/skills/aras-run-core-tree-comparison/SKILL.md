---
name: aras-run-core-tree-comparison
description: Use when Codex needs to start or inspect a controlled Core Tree comparison attempt for a known Aras upgrade case, including case/version validation, immutable snapshot, retry safety, directory lease, history, and a fixed result; stop when the formal CoreTreeComparisonCommand/action is unavailable.
---

# Run Core Tree Comparison

## Goal

Coordinate one case-bound Core Tree comparison attempt. This Skill owns the execution boundary; `aras-compare-core-tree` owns comparison rules and classification. It does not merge, modify, or release the R38 Core Tree, and it never generates a program while running.

## Required reading

Before any attempt, read `AGENTS.md`, `CONTEXT.md`, the relevant ADRs, `.scratch/aras-upgrade-orchestrator/spec.md` sections 14, 17, and 18, `docs/design/skill-map.md`, `docs/standards/AML_Structure_and_Traversal_Standard.md`, `aras-manage-upgrade-case`, and `aras-compare-core-tree`. Use `references/core-capabilities.md` as the dependency map.

## Fixed execution procedure

1. Read the case manifest and its Core Tree settings. Do not infer settings from a folder name or chat text.
2. Validate the case identifier, source/target versions, the three input roots, version evidence, and fixed Server text rules. A missing or mismatched input blocks the attempt.
3. Create an immutable 執行快照 containing case, task, action, route/version, target, input paths, tool identity, and checksums. 快照不可覆寫；a new attempt must not overwrite an older snapshot.
4. Create a 新的執行嘗試. Treat an unfinished prior attempt as `Interrupted`; retry only when the case store records the required evidence and retry safety conditions.
5. Apply the safety policy and acquire a `DirectoryLeaseManager` lease before reading/writing outputs. Overlapping roots, unsafe paths, or an existing lease block the attempt.
6. Require the formal command/action (`CoreTreeComparisonCommand`). If the 正式 command/action is unavailable, stop with `Blocked` and record why. Do not call `CoreTreeComparisonBuilder` directly to bypass the case gate; the formal command is responsible for invoking the builder.
7. Append start, result, interruption, or error events to the append-only case history (`history.jsonl`). Never edit or delete an existing event; corrections are new events.
8. Return the fixed result contract: case ID, attempt ID, source/target versions, status, A/B/C counts, output path, manual-review items, evidence paths, and errors. `Completed` is valid only when the builder proves all required inputs and review conditions; otherwise return `Incomplete` or `Blocked`.

## Current boundary

This repository currently has no formal `CoreTreeComparisonCommand`, CLI, UI, or command/action registration. Therefore a real case execution must stop at step 6 with `Blocked`; this Skill does not auto-create the missing command, log entries, snapshots, or output directories. The existing .NET core tests may invoke core types with isolated fixtures, but that is not Skill execution and is not evidence for a customer case.

## Stop conditions

- case identity, route, version evidence, or one of the three inputs is missing or inconsistent;
- a snapshot would overwrite an existing snapshot or an attempt would reuse an attempt ID;
- the safety policy rejects the path, lease, action, or external boundary;
- the formal command/action is missing, unregistered, or does not match the snapshot;
- manual review, binary comparison limitations, or missing evidence prevents `Completed`.

## Security rules

- Never access a real customer, `K:` drive, DB, Aras Export, login session, or upgrade tool from this Skill.
- Never change R38 Core Tree files, merge customer content, or mark `Completed` by judgment.
- Never overwrite inputs, snapshots, attempts, history, or prior output. New attempts use new directories.
- AI may explain a blocked result and prepare a checklist, but不得自動產生 missing command/action or authorize an external operation.

## Common mistakes

| Mistake | Correct response |
|---|---|
| Treating `CoreTreeComparisonBuilder` as a command | Stop until `CoreTreeComparisonCommand` is formally registered. |
| Generating executable code from the Skill | Report the missing implementation; do not generate or run it. |
| Editing `history.jsonl` to repair a result | Append a correction event with the original event ID. |
| Reusing an interrupted attempt directory | Create a new attempt only after retry evidence is recorded. |
| Calling a local core test a customer execution | Label it as an isolated test fixture, not case evidence. |

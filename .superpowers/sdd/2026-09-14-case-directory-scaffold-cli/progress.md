# SDD ledger — plan: docs/superpowers/plans/2026-09-14-case-directory-scaffold-cli.md

| Tasks | Shared files or interfaces | Finding | Ruling |
|---|---|---|---|
| 1 and 2 | `CaseDirectoryScaffoldCommand.cs`, `CaseDirectoryScaffoldRequest`, `CaseDirectoryScaffoldPlan` | Task 2 consumes Task 1 validation contract and adds Apply. | Execute sequentially; Task 1 owns Validate contract. |
| 1 and 3 | `CaseDirectoryScaffoldRequest` | CLI must deserialize the Task 1 request unchanged. | Keep request a public positional record in `ArasUpgradeOrchestrator.Core.Cases`. |
| 2 and 3 | `CaseDirectoryScaffoldResult` | CLI Apply serializes Task 2 result. | Keep result a public record with status and output paths. |
| 3 and 4 | CLI command names | Documentation must describe implemented command names. | Task 4 follows Task 3. |
| 4 | Documentation test | The written plan requested source-text tests, which do not test behavior. | Ruling: omit source-text tests; cover behavior through real CLI UTF-8 tests and manually review documentation. Cost if wrong: documentation drift may require a future documentation-specific review. |

Ruling: The project editing policy prohibits worktrees, and the working tree contains unrelated changes. Delegate only the new Core command file; retain shared-file integration in the controller workspace. Cost if wrong: manual integration review is required before final verification.

Task 1: fix round 1/5 (3 addressed, 0 open — template values, fully-qualified root, Windows-ambiguous names; review clean)
Task 1: complete (no commit; scoped review clean)
Task 2: fix round 2/5 (normal Apply inventory mismatch fixed by consistent `/` normalization; extra artifacts and staging collision protections retained)
Task 2: complete (scoped re-review clean; integration runner confirmed scaffold and CLI tests pass)
Task 3: complete (fixed CLI request wiring and UTF-8 JSON boundary; integration tests pass)
Task 4: complete (migrated scaffold Prompt and external-root policy to fixed CLI workflow; removed generated-script execution procedure)

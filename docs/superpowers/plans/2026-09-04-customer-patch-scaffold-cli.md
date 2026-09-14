# Customer-Patch Preparation Scaffold CLI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task.

**Goal:** 以正式 `Package.Cli` 指令建立 Customer-Patch Comparison 的 preparation 目錄、可解析的 preparation plan 與每個跳點的 evidence request，取代暫存 PowerShell 腳本。

**Architecture:** 新增 Core 層 scaffold command，先驗證完整 request、在記憶體中建立並 round-trip 驗證所有 JSON，接著寫入同層 staging 目錄，最後以單一目錄移動發布至新的 preparation 目錄。Package CLI 僅負責讀取 request、呼叫 command 與回報結果。輸出中的比較雙方固定使用 `comparison.source`／`comparison.target`；`sourceVersion`／`targetVersion` 僅保留在 hop 的中繼資料。

**Tech Stack:** .NET 8、C#、System.Text.Json、既有 console test harness。

**Spec:** `docs/superpowers/specs/2026-09-04-customer-patch-scaffold-cli-design.md`

## Global constraints

- 不連線 DB、不操作 Aras Export、不讀寫 Package 內容。
- 不覆寫既有 preparation 目錄或既有輸出。
- 所有 plan 與 request 中的相對路徑都必須以 `package-upgrade/<preparationId>/` 開頭。
- `evidenceRelativePath` 必須是 evidence 目錄，不能包含 `patch-support-evidence.request.json` 檔名。
- source／target 是比較輸入角色；跳點版本僅作為 hop metadata，不能推導比較目錄。
- 任何驗證或 staging 寫入失敗時，不得建立最終 preparation 目錄；staging 保留供診斷。

## Task 1: Core scaffold command and red tests

**Files:**
- Create: `src/ArasUpgradeOrchestrator.Core/Packages/CustomerPatchPreparationScaffoldCommand.cs`
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

1. Add tests covering a successful three-hop scaffold, missing baseline actual version, duplicate hop slug, and an existing target directory.
2. Run the console test project and confirm the new tests fail before implementation.
3. Add public request/result records and a command that validates inputs, materializes plan and request JSON in memory, stages the output, validates staged JSON, then moves it to the target preparation directory.
4. Ensure each generated request has top-level `hopId`, `source`, and `target`; ensure plan hop metadata also contains `comparison.source` and `comparison.target`.
5. Rerun the console test project and confirm the Core tests pass.

## Task 2: Package CLI command and integration test

**Files:**
- Modify: `tools/ArasUpgradeOrchestrator.Package.Cli/Program.cs`
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

1. Add `--scaffold-customer-patch-preparation <request.json>` to the CLI usage and command dispatch.
2. Deserialize the request using the existing web JSON options and call the Core scaffold command.
3. Add a CLI integration test that writes a UTF-8 request containing `芯洲`, invokes the command, and verifies the resulting plan is nonempty and parseable.
4. Run the console test project and confirm the CLI integration test passes.

## Task 3: Prompt and specification alignment

**Files:**
- Modify: `docs/prompts/codex-package-preparation-prompt.md`
- Modify: `docs/design/package-comparison-preparation-design.md`

1. Replace temporary PowerShell scaffold instructions with the formal Package CLI command and its request contract.
2. State that UTF-8 JSON and .NET path handling preserve Unicode case-root names; do not require PowerShell Unicode code-point literals.
3. State that the scaffold creates the nonempty manifest and per-hop request drafts, while rule publication is a separate later stage.
4. Run the full console test project once more after documentation changes.

## Task 4: Final verification

**Files:** all touched files above.

1. Inspect each changed file for the requested command name, comparison model, and forbidden request layout.
2. Run the full console test project fresh.
3. Report exact test result and confirm no external `K:` case data was changed.

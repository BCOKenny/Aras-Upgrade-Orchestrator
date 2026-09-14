# Core Tree Evidence CLI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建立正式、可驗證且不覆寫的 Core Tree Evidence CLI 產生與驗證能力。

**Architecture:** Core 層提供 Evidence Set 掃描、SHA-256 建立、相容性驗證與 staging 提交；CLI 僅負責解析 request、呼叫 Core 能力並輸出 JSON。preflight 重用同一個驗證器，避免檔名存在即通過。

**Tech Stack:** .NET 8、C#、現有 Console CLI、既有 console-test harness。

**Spec:** `docs/superpowers/specs/2026-09-10-core-tree-evidence-cli-design.md`

## Global Constraints

- 只處理案件根目錄內的三份 Core Tree 與 Evidence 目錄。
- 不執行 Core Tree 比較、DB、Package、AML Import 或升級。
- `--evidence-write` 不覆寫既有 Evidence 文件。
- 任一 `Partial` 或 `StaleOrConflict` 必須阻擋整次寫入。
- 文件以 UTF-8 with BOM 產生；相對路徑使用 `/`。
- 專案文件使用繁體中文。
- 不建立分支、worktree 或 Git commit。

---

### Task 1: Core Evidence 狀態模型與唯讀驗證器

**Files:**

- Create: `src/ArasUpgradeOrchestrator.Core/CoreTrees/CoreTreeEvidenceCommand.cs`
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**

- Consumes: 三份輸入識別、版本、Tree path、Evidence path、操作者。
- Produces: `CoreTreeEvidenceResult`，每組含 `Missing`、`CompleteCompatible`、`Partial` 或 `StaleOrConflict`。

- [ ] **Step 1: 寫入失敗測試：全缺失、部分文件與 Tree 變動。**

```csharp
var preview = await command.PreviewAsync(request);
Assert.Equal(CoreTreeEvidenceSetStatus.Missing, preview.Inputs.Single().Status);
```

- [ ] **Step 2: 執行指定測試，確認因型別與 command 尚不存在而失敗。**

Run: `dotnet run --project tests/ArasUpgradeOrchestrator.Core.Tests -- --filter CoreTreeEvidence`

Expected: 編譯失敗，指出 `CoreTreeEvidenceCommand` 或狀態模型不存在。

- [ ] **Step 3: 實作最小唯讀模型、路徑／結構檢查與 SHA-256 逐筆驗證。**

```csharp
public Task<CoreTreeEvidenceResult> PreviewAsync(CoreTreeEvidenceRequest request, CancellationToken cancellationToken = default);
```

- [ ] **Step 4: 重跑指定測試，確認預覽狀態判定通過。**

- [ ] **Step 5: 保持測試綠燈後再進入寫入任務。**

### Task 2: Staging 寫入與原子提交

**Files:**

- Modify: `src/ArasUpgradeOrchestrator.Core/CoreTrees/CoreTreeEvidenceCommand.cs`
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**

- Consumes: Task 1 的 `CoreTreeEvidenceRequest` 與預檢結果。
- Produces: 每組 `Created` 或 `Reused`，失敗時提供 staging 完整路徑。

- [ ] **Step 1: 寫入失敗測試：全 Missing 寫入四檔、相容 Set 重用、任一 Partial 阻擋全體。**

```csharp
var written = await command.WriteAsync(request);
Assert.Equal(CoreTreeEvidenceOperationStatus.Completed, written.Status);
Assert.True(File.Exists(Path.Combine(evidencePath, "integrity.sha256")));
```

- [ ] **Step 2: 執行指定測試，確認因 `WriteAsync` 不存在或不符合預期而失敗。**

- [ ] **Step 3: 實作全體預檢、sibling staging、重掃核對與不覆寫提交。**

```csharp
public Task<CoreTreeEvidenceWriteResult> WriteAsync(CoreTreeEvidenceRequest request, CancellationToken cancellationToken = default);
```

- [ ] **Step 4: 重跑指定測試，確認成功、重用與阻擋情境均通過。**

- [ ] **Step 5: 針對寫入例外補測試，確認 staging 保留且不回報完成。**

### Task 3: CLI 介面與 preflight 共用驗證

**Files:**

- Modify: `tools/ArasUpgradeOrchestrator.CoreTree.Cli/Program.cs`
- Modify: `src/ArasUpgradeOrchestrator.Core/CoreTrees/CoreTreeComparisonPreflightCommand.cs`
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**

- Consumes: `--evidence-preview`、`--evidence-write`、`--verify-evidence` JSON request。
- Produces: JSON result 與明確退出碼；preflight 對不相容 Evidence 回傳穩定 issue。

- [ ] **Step 1: 寫入失敗測試：CLI argument 路由與 preflight 拒絕 stale Evidence。**
- [ ] **Step 2: 執行測試，確認新 flag 尚未路由且 stale Evidence 尚未阻擋。**
- [ ] **Step 3: 實作 CLI request／result DTO、三個 flag、JSON 輸出與退出碼；改由 preflight 使用共用驗證器。**
- [ ] **Step 4: 重跑指定測試，確認 CLI 與 preflight 行為正確。**

### Task 4: 更新操作文件與完整驗證

**Files:**

- Modify: `docs/operations/core-tree/codex-core-tree-evidence-prompt.md`
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

- [ ] **Step 1: 將文件中的臨時 PowerShell 指令改為正式 CLI request／命令流程。**
- [ ] **Step 2: 加入「完整 JSON 摘要與退出碼前不得宣告完成」的操作判定。**
- [ ] **Step 3: 執行 Evidence 相關測試與一次 `dotnet build`，確認文件指向的 CLI 可編譯。**
- [ ] **Step 4: 逐一檢查本次變更檔案，確認均直接支援 Evidence CLI 能力。**

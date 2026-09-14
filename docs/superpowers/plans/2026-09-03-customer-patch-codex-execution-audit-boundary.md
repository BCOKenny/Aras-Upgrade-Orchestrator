# Customer-Patch Codex 受控執行稽核邊界 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** 在具名人工核准與 receipt 完整驗證後，讓 Codex 透過受控 Package CLI 發布 Customer-Patch 共用規則，並保存 `approvedBy`、固定 `executedBy: CodexAgent` 與 request SHA-256。

**Architecture:** 保留既有人工發布命令作為相容入口，新增獨立的 Codex 受控執行 request、preflight 與發布命令。核心命令負責固定執行者、驗證 approval receipt 與 request 雜湊；CLI 僅序列化固定輸出。發布準備與執行 Prompt 改為產生／使用新 request，PowerShell 啟動器不再是必要步驟。

**Tech Stack:** .NET 8、C#、System.Text.Json、SHA-256、既有 `RuleSetStore`、自訂 Core 測試主程式、Markdown。

**Spec:** `docs/superpowers/specs/2026-09-03-customer-patch-codex-execution-audit-boundary-design.md`

## Global Constraints

- `approvedBy` 必須等於 approval receipt 的具名人工 `actor`，且 receipt 決定必須是 `Approved`。
- `executedBy` 固定為 `CodexAgent`，request 不得提供或覆寫此值。
- 先通過 receipt、approval draft Checksum、路徑與既有規則 preflight，才可建立 draft 或 published 規則。
- request SHA-256 必須依 CLI 實際讀取的 request 位元組計算並輸出。
- 保留既有 `--publish-customer-patch-common-rule` 的人工發布語意與相容性。
- Codex 只可在使用者對話明確確認發布後執行新命令；不得自行核准或手工寫入規則檔。
- 不建立 Git branch、worktree 或 commit；這是專案本機編輯政策。
- 文件一律使用繁體中文。

---

## 檔案結構

- Create: `src/ArasUpgradeOrchestrator.Core/Rules/CustomerPatchCodexExecutionCommand.cs`：新增 Codex 受控執行 request、preflight、結果與發布命令。
- Modify: `src/ArasUpgradeOrchestrator.Core/Rules/RuleSetModels.cs`：新增 optional `RuleExecutionAudit` 並保存至 published 規則。
- Modify: `src/ArasUpgradeOrchestrator.Core/Rules/RuleSetStore.cs`：讓受控發布能原子寫入 optional `RuleExecutionAudit`。
- Modify: `tools/ArasUpgradeOrchestrator.Package.Cli/Program.cs`：新增 `--execute-customer-patch-common-rule` JSON CLI 入口與輸出。
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`：新增成功與拒絕案例，並驗證 CLI JSON 結果。
- Modify: `docs/operations/case-management/codex-customer-patch-rule-publication-preparation-prompt.md`：產生 `approvedBy` request，保留核准收據。
- Modify: `docs/operations/case-management/codex-customer-patch-rule-publication-execution-prompt.md`：改為 Codex 受控發布確認 Prompt，移除 PowerShell 必要步驟。
- Modify: `docs/superpowers/specs/2026-09-02-customer-patch-rule-publication-launcher-design.md`：將 PowerShell 啟動器標記為淘汰的相容工具。

### Task 1: 定義 Codex 受控執行核心介面與拒絕測試

**Files:**
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`
- Create: `src/ArasUpgradeOrchestrator.Core/Rules/CustomerPatchCodexExecutionCommand.cs`

**Interfaces:**
- Consumes: `CustomerPatchRulePublicationApprovalReceiptStore.LoadAndValidateAsync(...)`、`RuleSetStore`、`DefaultUpgradeRuleSets.CreateCustomerPatchComparisonDraft(...)`。
- Produces: `CustomerPatchCodexExecutionRequest(string CaseRoot, string RuleStoreRelativePath, string ApprovalEvidenceRelativePath, string ApprovalReceiptRelativePath, string ApprovedBy, byte[] RequestBytes)`、`CustomerPatchCodexExecutionResult(..., string ApprovedBy, string ExecutedBy, string RequestChecksum)`、`CustomerPatchCodexExecutionCommand.ExecuteAsync(...)`。

- [x] **Step 1: 寫入失敗測試：receipt 人員與 `approvedBy` 不一致時零寫入**

在 Core 測試主程式加入案例，建立有效 draft 與 receipt，但以不同 `ApprovedBy` 呼叫：

```csharp
await AssertThrowsAsync<InvalidDataException>(() => new CustomerPatchCodexExecutionCommand().ExecuteAsync(
    new(caseRoot, "rule-sets", draftRelativePath, receiptRelativePath, "BCO\\other")));
Assert.False(Directory.Exists(Path.Combine(ruleStoreRoot, "drafts")));
Assert.False(Directory.Exists(Path.Combine(ruleStoreRoot, "published")));
```

- [x] **Step 2: 執行測試，確認因類別不存在而失敗**

Run: `dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests\ArasUpgradeOrchestrator.Core.Tests.csproj`

Expected: 編譯失敗，指出 `CustomerPatchCodexExecutionCommand` 不存在。

- [x] **Step 3: 實作最小 request、preflight 與 receipt 驗證**

建立下列核心型別與邏輯：

```csharp
public sealed record CustomerPatchCodexExecutionRequest(
    string CaseRoot, string RuleStoreRelativePath, string ApprovalEvidenceRelativePath,
    string ApprovalReceiptRelativePath, string ApprovedBy, byte[] RequestBytes);

public sealed class CustomerPatchCodexExecutionCommand
{
    public async Task<CustomerPatchCodexExecutionResult> ExecuteAsync(
        CustomerPatchCodexExecutionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ApprovedBy))
            throw new ArgumentException("Codex 受控發布必須指定具名核准人。", nameof(request));
        if (request.RequestBytes is null || request.RequestBytes.Length == 0)
            throw new ArgumentException("Codex 受控發布缺少 request 原始內容。", nameof(request));
        var preflight = await new CustomerPatchCommonRulePublicationPreflightCommand().ExecuteAsync(
            new(request.CaseRoot, request.RuleStoreRelativePath, request.ApprovalEvidenceRelativePath,
                request.ApprovalReceiptRelativePath, request.ApprovedBy), cancellationToken);
        // 其後以 RuleActor(request.ApprovedBy, RuleActorKind.Human) 發布固定 draft。
    }
}
```

重用既有 `LoadAndValidateAsync(... expectedActor ...)`，將 `request.ApprovedBy` 傳入 `expectedActor`；不得改變既有人工發布命令的 receipt 驗證語意。

- [x] **Step 4: 執行測試，確認拒絕案例通過**

Run: `dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests\ArasUpgradeOrchestrator.Core.Tests.csproj`

Expected: 新增案例通過，且既有案例仍通過。

### Task 2: 完成發布、不可偽造執行者與 request Checksum 測試

**Files:**
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`
- Modify: `src/ArasUpgradeOrchestrator.Core/Rules/CustomerPatchCodexExecutionCommand.cs`
- Modify: `src/ArasUpgradeOrchestrator.Core/Rules/RuleSetModels.cs`
- Modify: `src/ArasUpgradeOrchestrator.Core/Rules/RuleSetStore.cs`

**Interfaces:**
- Consumes: Task 1 的 `CustomerPatchCodexExecutionCommand`。
- Produces: 成功結果固定含 `ApprovedBy`、`ExecutedBy = "CodexAgent"` 與實際 request SHA-256。

- [x] **Step 1: 寫入失敗測試：任意執行者不得進入 request**

以 JSON 反序列化包含 `executedBy: "BCO\\other"` 的 request，並驗證 CLI request DTO 拒絕未知欄位或核心 request 不包含可設定執行者欄位。測試名稱明確表達「Codex 執行者不可由 request 覆寫」。

- [x] **Step 2: 寫入失敗測試：已發布同類規則不可重複執行**

先使用有效核准資料執行一次，再次呼叫同一 request：

```csharp
await command.ExecuteAsync(request);
await AssertThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync(request));
```

- [x] **Step 3: 執行測試，確認新案例失敗**

Run: `dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests\ArasUpgradeOrchestrator.Core.Tests.csproj`

Expected: 新增成功輸出／重複拒絕／request schema 案例尚未通過。

- [x] **Step 4: 實作最小發布與稽核結果**

在 `RuleSetModels.cs` 新增：

```csharp
public sealed record RuleExecutionAudit(
    string ApprovedBy, string ExecutedBy, string ApprovalReceiptReference, string RequestChecksum);
```

將 `RuleExecutionAudit? ExecutionAudit = null` 加入 `PublishedRuleSet`，維持舊 published JSON 的反序列化相容。`RuleSetStore.PublishAsync` 新增 optional `RuleExecutionAudit? executionAudit = null` 參數，並將它交給 `PublishedRuleSet.Create(...)`；仍使用同一個 `FileMode.CreateNew` published 寫入。

發布命令使用 `RuleActor(request.ApprovedBy, RuleActorKind.Human)` 建立 draft 與 `RulePublicationApproval`；結果額外固定：

```csharp
public sealed record CustomerPatchCodexExecutionResult(
    Guid CaseId, string RuleStorePath, string ApprovalEvidencePath, string ApprovalReceiptPath,
    Guid DraftId, PublishedRuleSet PublishedRuleSet, string ApprovedBy,
    string ExecutedBy, string RequestChecksum);
```

`ExecutedBy` 常數為 `CodexAgent`。使用 `Convert.ToHexString(SHA256.HashData(request.RequestBytes))` 計算 `RequestChecksum`；不得重新序列化 request 後再計算。以此四欄位建立 `RuleExecutionAudit`，傳給 `RuleSetStore.PublishAsync`，並在測試斷言讀回的 `PublishedRuleSet.ExecutionAudit` 完全一致。

- [x] **Step 5: 執行測試，確認發布與拒絕案例通過**

Run: `dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests\ArasUpgradeOrchestrator.Core.Tests.csproj`

Expected: 成功結果含固定 `CodexAgent` 與預期 SHA-256；不一致、重複與任意執行者案例均拒絕。

### Task 3: 新增受控 CLI 命令與 JSON 邊界測試

**Files:**
- Modify: `tools/ArasUpgradeOrchestrator.Package.Cli/Program.cs`
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**
- Consumes: `CustomerPatchCodexExecutionCommand.ExecuteAsync(...)` 與 request 原始檔案位元組。
- Produces: `--preflight-codex-customer-patch-common-rule <request.json>` 與 `--execute-customer-patch-common-rule <request.json>`；前者零寫入，後者成功輸出 `approvedBy`、`executedBy`、`requestChecksum`、`publishedRuleSet`。

- [x] **Step 1: 寫入 CLI 失敗測試：request 含 `executedBy` 必須回傳 exit code 1**

建立 request JSON，加入未允許的 `executedBy` 欄位，呼叫：

```csharp
var result = await RunPackageCliAsync("--execute-customer-patch-common-rule", requestPath);
Assert.Equal(1, result.ExitCode);
Assert.Contains("executedBy", result.StandardError, StringComparison.OrdinalIgnoreCase);
```

- [x] **Step 2: 執行測試，確認新 CLI 命令尚未支援而失敗**

Run: `dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests\ArasUpgradeOrchestrator.Core.Tests.csproj`

Expected: CLI exit code 與錯誤訊息不符合預期。

- [x] **Step 3: 實作 CLI command 與嚴格 request DTO 驗證**

新增 CLI DTO：

```csharp
public sealed record CustomerPatchCodexExecutionCliRequest(
    string CaseRoot, string RuleStoreRelativePath, string ApprovalEvidenceRelativePath,
    string ApprovalReceiptRelativePath, string ApprovedBy);
```

以 `JsonDocument.Parse(requestBytes)` 檢查只允許 `caseRoot`、`ruleStoreRelativePath`、`approvalEvidenceRelativePath`、`approvalReceiptRelativePath` 與 `approvedBy` 五個欄位；遇到 `executedBy` 或其他未知欄位時，輸出 `CliInputError` 並回傳 1。以 `File.ReadAllBytesAsync(args[1])` 取得原始 request 位元組，傳入核心命令計算 `RequestChecksum`。

- [x] **Step 4: 新增 CLI preflight／成功測試並驗證輸出欄位**

測試 request 含相符 `approvedBy` 與 receipt；先呼叫 Codex preflight 並斷言 exit code 0、未建立 draft，再呼叫受控發布，斷言 JSON 的 `executedBy` 等於 `CodexAgent`、`approvedBy` 等於 `BCO\\kenny`、`requestChecksum` 等於 request 檔案 SHA-256。

- [x] **Step 5: 執行測試，確認 CLI 所有案例通過**

Run: `dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests\ArasUpgradeOrchestrator.Core.Tests.csproj`

Expected: 新 CLI 成功與拒絕案例通過，且既有 CLI 命令維持相容。

### Task 4: 更新發布準備與執行文件

**Files:**
- Modify: `docs/operations/case-management/codex-customer-patch-rule-publication-preparation-prompt.md`
- Modify: `docs/operations/case-management/codex-customer-patch-rule-publication-execution-prompt.md`
- Modify: `docs/superpowers/specs/2026-09-02-customer-patch-rule-publication-launcher-design.md`

**Interfaces:**
- Consumes: 新 request 的 `approvedBy`、CLI `--execute-customer-patch-common-rule`、固定輸出 `CodexAgent`。
- Produces: 使用者可貼入的「確認發布 Customer-Patch 共用規則」Prompt 與無 PowerShell 的受控執行說明。

- [x] **Step 1: 修改準備 Prompt 的 request 範例與產生規則**

將 `actor` 改為 `approvedBy`，並明確禁止 request 包含 `executedBy`。核准 receipt 的 `actor` 必須由具名人工核准人固定帶入。

- [x] **Step 2: 修改執行 Prompt 的最少輸入與確認語句**

最少輸入只保留 `caseRoot` 與 `approvedBy`，並提供：

```text
確認發布 Customer-Patch 共用規則。
案件根目錄：<caseRoot>
核准人：<approvedBy>
```

明確說明 Codex 先執行唯讀 preflight，通過後才以 `CodexAgent` 執行受控 CLI；不得要求 PowerShell、`.cmd` 或手工命令。

- [x] **Step 3: 標記舊版啟動器設計為非必要相容工具**

在舊 launcher 設計文件頂端加入醒目註記：Customer-Patch 正式發布已改由 Codex 受控 CLI 執行，PowerShell 啟動器不得作為必要發布步驟。

- [x] **Step 4: 唯讀檢查文件一致性**

Run: `rg -n "Publish-CustomerPatchRule|PowerShell|executedBy|approvedBy|execute-customer-patch-common-rule" docs\operations\case-management docs\superpowers\specs`

Expected: 正式準備與執行文件僅引用 `approvedBy` 與 Codex 受控 CLI；舊 launcher 僅在淘汰／相容說明出現。

### Task 5: 完整驗證與交付檢查

**Files:**
- Verify: `src/ArasUpgradeOrchestrator.Core/Rules/CustomerPatchCodexExecutionCommand.cs`
- Verify: `tools/ArasUpgradeOrchestrator.Package.Cli/Program.cs`
- Verify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`
- Verify: `docs/operations/case-management/codex-customer-patch-rule-publication-*.md`

**Interfaces:**
- Consumes: Tasks 1–4 的完整實作。
- Produces: 可執行的 Codex 受控發布流程與可追溯輸出。

- [x] **Step 1: 執行完整 Core 測試**

Run: `dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests\ArasUpgradeOrchestrator.Core.Tests.csproj`

Expected: 全部測試通過，包含新 Codex 受控執行、request schema、receipt 不一致與重複發布案例。

- [x] **Step 2: 執行一次建置**

Run: `dotnet build --verbosity:minimal`

Expected: 0 errors；本使用者請求中只執行一次。

- [x] **Step 3: 檢查變更範圍，不使用 Git**

Run: `Get-ChildItem` 與 `rg --files` 僅確認本計畫列出的 Core、CLI、測試與文件檔案。

Expected: 沒有案件目錄 `K:` 的寫入、沒有手工建立 `published` 規則、沒有 Git branch 或 worktree 操作。

## 自我審查

- Spec coverage：Task 1–3 實作資料與命令邊界、Task 4 實作對話與文件邊界、Task 5 驗證所有拒絕與成功條件。
- Placeholder scan：本計畫沒有待辦標記、未定義介面或模糊實作步驟；角括號只在 CLI usage 與使用者輸入範例中表示參數。
- Type consistency：核心 request 使用 `ApprovedBy`，CLI DTO 使用 `ApprovedBy`，結果使用 `ApprovedBy`、固定 `ExecutedBy` 與 `RequestChecksum`。

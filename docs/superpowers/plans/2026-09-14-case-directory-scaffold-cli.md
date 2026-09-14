# Case Directory Scaffold CLI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建立可驗證的 .NET CLI，安全建立 `DIRECTORY_SCAFFOLD_ONLY` 案件目錄與非正式範本，取代每次由模型產生的臨時 PowerShell。

**Architecture:** Core 層的 `CaseDirectoryScaffoldCommand` 在記憶體驗證 request、推導所有輸出與範本，並提供零寫入 Validate 與 staging 發布 Apply。CoreTree CLI 只負責讀取 UTF-8 request、呼叫 command、輸出結構化 JSON 與映射退出碼；操作 Prompt 只引用此固定介面。

**Tech Stack:** .NET 8、C#、System.Text.Json、既有自訂整合測試主程式、UTF-8 檔案 I/O。

**Spec:** `docs/superpowers/specs/2026-09-14-case-directory-scaffold-cli-design.md`

## Global Constraints

- 僅接受 `DIRECTORY_SCAFFOLD_ONLY`；不得建立或讀取 `aras-upgrade-case.json`、`.orchestrator\history.jsonl` 或 `CaseManifest`。
- 不得執行 DB、SQL、AML Import、Package Import、Aras Export、Core Tree preflight 或升級。
- 預檢失敗時不得覆寫、合併、刪除或重試任何外部案件資料。
- Validate 必須零寫入；Apply 必須在同層 staging 完整驗證後才以一次 `Directory.Move` 發布。
- 所有新增或更新的文件使用繁體中文；不建立 Git repository、分支或 worktree。
- 不執行 `dotnet test`；驗證使用既有 `dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests\ArasUpgradeOrchestrator.Core.Tests.csproj`。

---

## File structure

| 檔案 | 責任 |
|---|---|
| `src/ArasUpgradeOrchestrator.Core/Cases/CaseDirectoryScaffoldCommand.cs` | request 驗證、輸出計畫、範本內容、staging 建立與發布。 |
| `tools/ArasUpgradeOrchestrator.CoreTree.Cli/Program.cs` | `--validate-case-directory-scaffold` 與 `--scaffold-case-directory` 的 JSON 邊界、退出碼與 help。 |
| `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs` | command 與 CLI 的整合測試。 |
| `docs/operations/case-management/codex-case-scaffold-prompt.md` | 將操作流程改為固定 request/CLI，不再產生 PowerShell。 |
| `docs/operations/case-management/external-case-root-policy.md` | 對齊固定 CLI 執行面與非正式骨架產出。 |

### Task 1: 建立零寫入驗證契約與失敗測試

**Files:**
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`
- Create: `src/ArasUpgradeOrchestrator.Core/Cases/CaseDirectoryScaffoldCommand.cs`

**Interfaces:**
- Consumes: `CaseDirectoryScaffoldRequest(string CaseRoot, string CaseDirectoryName, string CustomerCode, string SourceVersion, string SourceSlug, string TargetVersion, string TargetSlug, string Actor, string ExecutionMode)`。
- Produces: `CaseDirectoryScaffoldPlan`，其中包含 `TargetPath`、20 個 `DirectoryPaths`、7 個 `TemplatePaths`、`ForbiddenFormalPaths` 與 `ScaffoldIdentifier`。

- [ ] **Step 1: 寫入預期失敗的 Validate 整合測試**

在測試清單加入 `CaseDirectoryScaffoldValidateIsZeroWriteAndUnicodeSafe`，建立只含外部根目錄的暫存 fixture，使用「芯洲」作為 `CaseDirectoryName` 與 `CustomerCode`，呼叫尚不存在的 `new CaseDirectoryScaffoldCommand().Validate(request)`，並斷言：

```csharp
Assert.Equal("DIRECTORY_SCAFFOLD_ONLY", plan.ExecutionMode);
Assert.Equal(Path.Combine(root, "芯洲"), plan.TargetPath);
Assert.Equal(20, plan.DirectoryPaths.Count);
Assert.Equal(7, plan.TemplatePaths.Count);
Assert.False(Directory.Exists(plan.TargetPath));
Assert.False(File.Exists(Path.Combine(plan.TargetPath, "aras-upgrade-case.json")));
Assert.False(File.Exists(Path.Combine(plan.TargetPath, ".orchestrator", "history.jsonl")));
```

另加入 `CaseDirectoryScaffoldRejectsUnsafeRequestsWithoutWrites`，逐一驗證錯誤 execution mode、相同版本、非小寫 slug、`<待提供>`、`..`、目錄分隔符與既有案件目錄皆拋出 `InvalidDataException` 或 `InvalidOperationException`，且根目錄快照不變。

- [ ] **Step 2: 執行測試，確認因缺少 command 而失敗**

Run:

```powershell
dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests\ArasUpgradeOrchestrator.Core.Tests.csproj
```

Expected: 編譯失敗並明確指出 `CaseDirectoryScaffoldCommand` 或其 request/plan type 尚不存在。

- [ ] **Step 3: 實作最小的 Validate-only Core command**

在 `CaseDirectoryScaffoldCommand.cs` 定義上述 record、`CaseDirectoryScaffoldTemplate(string RelativePath, string Content)` 與 `CaseDirectoryScaffoldPlan`。實作 `Validate`：

```csharp
public CaseDirectoryScaffoldPlan Validate(CaseDirectoryScaffoldRequest request)
{
    ArgumentNullException.ThrowIfNull(request);
    var caseRoot = RequireExistingDirectory(request.CaseRoot, nameof(request.CaseRoot));
    ValidateRequest(request);
    var targetPath = RequireDirectChild(caseRoot, request.CaseDirectoryName);
    var plan = BuildPlan(caseRoot, targetPath, request);
    RejectExistingCaseData(plan);
    ValidatePlannedTemplates(plan);
    return plan;
}
```

`BuildPlan` 必須從目前 Prompt 的固定清單建立 20 個目錄與 7 個範本；JSON 範本用 `JsonSerializer.Serialize` 後 `JsonDocument.Parse` round-trip，Markdown 以 `Encoding.UTF8.GetBytes`／`GetString` round-trip。不得建立任何目錄或檔案。

- [ ] **Step 4: 執行測試，確認 Validate 測試通過**

Run:

```powershell
dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests\ArasUpgradeOrchestrator.Core.Tests.csproj
```

Expected: 新增的 Validate／拒絕測試與既有測試全部通過。

### Task 2: 以 staging 實作 Apply 與產出完整性測試

**Files:**
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`
- Modify: `src/ArasUpgradeOrchestrator.Core/Cases/CaseDirectoryScaffoldCommand.cs`

**Interfaces:**
- Consumes: Task 1 的 `CaseDirectoryScaffoldRequest` 與 `CaseDirectoryScaffoldPlan`。
- Produces: `Task<CaseDirectoryScaffoldResult> ApplyAsync(CaseDirectoryScaffoldRequest request, CancellationToken cancellationToken = default)`，結果固定包含 `Status`、`TargetPath`、`ScaffoldIdentifier`、`DirectoryPaths`、`TemplatePaths` 與可空 `StagingPath`。

- [ ] **Step 1: 寫入預期失敗的 Apply 整合測試**

加入 `CaseDirectoryScaffoldApplyCreatesOnlyNonFormalArtifacts`，呼叫尚不存在的 `ApplyAsync` 並斷言：

```csharp
Assert.Equal("Created", result.Status);
Assert.True(Directory.Exists(result.TargetPath));
Assert.Equal(20, result.DirectoryPaths.Count);
Assert.Equal(7, result.TemplatePaths.Count);
Assert.False(File.Exists(Path.Combine(result.TargetPath, "aras-upgrade-case.json")));
Assert.False(File.Exists(Path.Combine(result.TargetPath, ".orchestrator", "history.jsonl")));
Assert.True(result.TemplatePaths.All(File.Exists));
foreach (var jsonPath in result.TemplatePaths.Where(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
    using (JsonDocument.Parse(await File.ReadAllTextAsync(jsonPath))) { }
```

測試同時檢查所有產出是 `TargetPath` 之下的相對路徑，並檢查目標根目錄沒有 staging 目錄殘留於成功情況。

- [ ] **Step 2: 執行測試，確認因缺少 Apply 而失敗**

Run:

```powershell
dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests\ArasUpgradeOrchestrator.Core.Tests.csproj
```

Expected: 編譯失敗並明確指出 `ApplyAsync` 或 `CaseDirectoryScaffoldResult` 尚不存在。

- [ ] **Step 3: 實作 staging Apply**

實作：

```csharp
public async Task<CaseDirectoryScaffoldResult> ApplyAsync(
    CaseDirectoryScaffoldRequest request,
    CancellationToken cancellationToken = default)
{
    var plan = Validate(request);
    var stagingPath = Path.Combine(plan.CaseRoot, $".{request.CaseDirectoryName}.scaffold-staging-{Guid.NewGuid():N}");
    Directory.CreateDirectory(stagingPath);
    try
    {
        await WritePlanToStagingAsync(stagingPath, plan, cancellationToken);
        await ValidateStagingAsync(stagingPath, plan, cancellationToken);
        Directory.Move(stagingPath, plan.TargetPath);
        return CaseDirectoryScaffoldResult.Created(plan);
    }
    catch
    {
        throw;
    }
}
```

`WritePlanToStagingAsync` 必須採 `FileMode.CreateNew` 與 UTF-8 無 BOM；`ValidateStagingAsync` 必須確認所有 20 個目錄、7 個範本與兩個禁止正式檔案的狀態。catch 不得刪除 staging 或重試。

- [ ] **Step 4: 執行測試，確認 Apply 測試通過**

Run:

```powershell
dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests\ArasUpgradeOrchestrator.Core.Tests.csproj
```

Expected: 新增 Apply 測試與既有測試全部通過，且成功案例不留下 staging 目錄。

### Task 3: 接上 CoreTree CLI 並測試 UTF-8 request 邊界

**Files:**
- Modify: `tools/ArasUpgradeOrchestrator.CoreTree.Cli/Program.cs`
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**
- Consumes: `--validate-case-directory-scaffold <request.json>`、`--scaffold-case-directory <request.json>` 與 Task 1 request。
- Produces: Validate 成功 exit code 0 與 JSON plan；Apply 成功 exit code 0 與 JSON result；request 或安全失敗為 JSON `CliInputError` 並 exit code 1。

- [ ] **Step 1: 寫入預期失敗的 CLI 測試**

加入 `CoreTreeCliValidatesCaseDirectoryScaffoldWithoutMutation` 與 `CoreTreeCliCreatesCaseDirectoryScaffoldFromUtf8Request`。第一個建立 UTF-8 request，呼叫 `RunCoreTreeCliAsync("--validate-case-directory-scaffold", requestPath)`，斷言 exit code 0、`status` 為 `Validated` 且目標不存在。第二個以相同 request 呼叫 `--scaffold-case-directory`，斷言 exit code 0、`status` 為 `Created`、Unicode 目標存在且正式禁止檔案不存在。

- [ ] **Step 2: 執行測試，確認 CLI command 尚未存在而失敗**

Run:

```powershell
dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests\ArasUpgradeOrchestrator.Core.Tests.csproj
```

Expected: CLI 回傳 usage／argument error，因尚未接受兩個新 command。

- [ ] **Step 3: 新增 CLI command 分支與 help**

在 `Program.cs` 的正式 Core Tree command 前新增分支，讀取 request JSON、反序列化 `CaseDirectoryScaffoldRequest`、呼叫 `Validate` 或 `ApplyAsync` 並序列化結果。help 與 usage error 必須包含兩個新 command。捕捉既有的 `IOException`、`UnauthorizedAccessException`、`JsonException`、`InvalidDataException`、`ArgumentException` 與 `InvalidOperationException`，使用既有 `WriteFailureAsync("CliInputError", ...)`。

- [ ] **Step 4: 執行測試，確認 CLI contract 通過**

Run:

```powershell
dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests\ArasUpgradeOrchestrator.Core.Tests.csproj
```

Expected: 新增 CLI 測試與既有測試全部通過；CLI validate 保持零寫入，apply 只建立非正式骨架。

### Task 4: 遷移操作文件並驗證固定執行面

**Files:**
- Modify: `docs/operations/case-management/codex-case-scaffold-prompt.md`
- Modify: `docs/operations/case-management/external-case-root-policy.md`
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**
- Consumes: Task 3 的兩個 CLI command 與 request schema。
- Produces: 操作文件只要求固定 JSON request 與 CLI validate/apply，不含可執行 PowerShell 或「建立 `aras-upgrade-case.json`」的 DIRECTORY_SCAFFOLD_ONLY 說明。

- [ ] **Step 1: 寫入預期失敗的文件契約測試**

加入 `CaseDirectoryScaffoldPromptUsesFixedCliBoundary`，讀取兩份 Markdown，斷言兩者均出現 `--validate-case-directory-scaffold` 與 `--scaffold-case-directory`，Prompt 不含 `PowerShell`、`[char]::ConvertFromUtf32`、`ExecutionPolicy Bypass` 或 `String.raw`，且 policy 的 DIRECTORY_SCAFFOLD_ONLY 範例不將 `aras-upgrade-case.json` 作為建立產出。

- [ ] **Step 2: 執行測試，確認舊文件契約失敗**

Run:

```powershell
dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests\ArasUpgradeOrchestrator.Core.Tests.csproj
```

Expected: 文件契約測試失敗，指出目前 Prompt 仍含 PowerShell 與 policy 仍含不正確正式案件清單產出。

- [ ] **Step 3: 更新兩份操作文件**

將 Prompt 的 DIRECTORY_SCAFFOLD_ONLY 寫入規則替換為：建立固定 schema request、唯讀執行 validate、確認使用者對固定根目錄授權、以 CLI apply 建立骨架、讀取 JSON 結果。保留禁止 DB／Package／Core Tree 作業與人工後續動作；刪除所有 PowerShell、BOM、ASCII、code point、反引號與編排字串規則。

更新外部根目錄政策，將 CLI 定義為固定執行面而非額外前置條件；把該模式可建立產出更正為 `.orchestrator`、Core Tree／Package／DB 工作目錄及非正式範本，不得列出 `aras-upgrade-case.json`。

- [ ] **Step 4: 執行完整測試與文件掃描**

Run:

```powershell
dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests\ArasUpgradeOrchestrator.Core.Tests.csproj
rg -n "PowerShell|ConvertFromUtf32|ExecutionPolicy Bypass|String\.raw" docs\operations\case-management\codex-case-scaffold-prompt.md docs\operations\case-management\external-case-root-policy.md
```

Expected: 測試主程式 exit code 0；掃描對這兩份文件沒有可執行 PowerShell 傳遞規則。

## Plan self-review

- Spec coverage：Task 1 覆蓋 request、零寫入、路徑與禁止檔驗證；Task 2 覆蓋 staging／發布與非正式產出；Task 3 覆蓋 UTF-8 CLI 邊界；Task 4 覆蓋 Prompt 與外部根目錄政策遷移。
- Placeholder scan：本計畫沒有 `TODO`、`TBD` 或未定義的實作步驟。
- Type consistency：Task 1 定義 `CaseDirectoryScaffoldRequest`／`CaseDirectoryScaffoldPlan`；Task 2 使用它們並定義 `CaseDirectoryScaffoldResult`；Task 3 與 Task 4 只使用前三個已定義契約。

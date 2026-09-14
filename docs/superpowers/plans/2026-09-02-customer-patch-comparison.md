# Customer-Patch Comparison Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建立客戶 Package 基準對各跳 Patch／Support 的受控 XML 比較、候選輸出與核定 CLI，同時保留既有 OOTB Rule 1／Rule 2 行為。

**Architecture:** 新能力以獨立 `CustomerPatchComparison` Policy、唯讀 preflight、不可覆寫 comparison attempt 及核定 command 組成。比較 command 只讀取客戶基準與 `patch-support-input`，在新 attempt 寫入 `CustomerComparisonCopy`、`PatchCandidateCopy` 與 receipt；核定 command 才複製候選 XML 至新的 `candidate-package-imports/<candidate-id>`，且不寫入正式 `Support\\Solutions`。

**Tech Stack:** .NET 8、既有 `AmlDocument`／`AmlSemanticComparer`／`PackageXmlPathMatcher`、`CaseStore`、`AppendOnlyHistoryStore`、`DirectoryLeaseManager`、`SafetyPolicy`、自訂 console test runner。

**Spec:** `docs/superpowers/specs/2026-09-02-customer-patch-comparison-design.md`

## Global Constraints

- 客戶基準、`patch-support-input`、OOTB 輸入及正式升級工具均為唯讀。
- 僅比較 `.xml`；非 XML 不解析、不比較、不複製到候選輸出。
- 相同相對路徑 XML 只有 SHA-256 相同時才從 `PatchCandidateCopy` 排除。
- 位元不同 XML 即使 AML 語意相同也必須保留於兩側比較副本。
- Item 四種結果只供報告與人工確認，不得刪除 Item。
- 每次比較與核定使用全新、不重疊輸出目錄；歷程只追加。
- 新功能不得改寫既有 OOTB Rule 1／Rule 2 command、receipt、ZIP 或 CLI request。
- AI 不得發布 Policy、解除人工確認、核定候選或修改正式 `Support\\Solutions`。
- 所有 AML 行為遵守 `docs/standards/AML_Structure_and_Traversal_Standard.md`。

---

## File structure

| 檔案 | 職責 |
|---|---|
| `src/ArasUpgradeOrchestrator.Core/Rules/RuleSetModels.cs` | 新增 `CustomerPatchComparison` Policy 類型與保留步驟。 |
| `src/ArasUpgradeOrchestrator.Core/Rules/RuleSetValidator.cs` | 驗證新 Policy 只使用固定的保留步驟。 |
| `src/ArasUpgradeOrchestrator.Core/Rules/DefaultUpgradeRuleSets.cs` | 提供可供人工發布的 Customer-Patch Policy 草稿。 |
| `src/ArasUpgradeOrchestrator.Core/Packages/CustomerPatchComparisonModels.cs` | request、狀態、檔案／Item 報告、receipt 與核定資料模型。 |
| `src/ArasUpgradeOrchestrator.Core/Packages/CustomerPatchEvidenceRegistrationCommand.cs` | 以具名 actor 與實際時間登錄 Patch／Support 證據及輸入摘要。 |
| `src/ArasUpgradeOrchestrator.Core/Packages/CustomerPatchComparisonPreflightCommand.cs` | 唯讀輸入、證據、Policy、路徑與輸出隔離檢查。 |
| `src/ArasUpgradeOrchestrator.Core/Packages/CustomerPatchComparisonBuilder.cs` | XML hash、檔案配對、AML 報告與 attempt 工作副本建立。 |
| `src/ArasUpgradeOrchestrator.Core/Packages/CustomerPatchComparisonCommand.cs` | 安全判定、鎖、history、輸入摘要與不可覆寫 receipt 協調。 |
| `src/ArasUpgradeOrchestrator.Core/Packages/CustomerPatchComparisonReceiptStore.cs` | receipt 寫入／讀取及不可覆寫保護。 |
| `src/ArasUpgradeOrchestrator.Core/Packages/CustomerPatchComparisonApprovalCommand.cs` | 重新核對摘要／Policy，建立新的已核定候選輸出。 |
| `src/ArasUpgradeOrchestrator.Core/Execution/History.cs` | 新比較開始、待審閱、待核定、核定及阻擋事件。 |
| `tools/ArasUpgradeOrchestrator.Package.Cli/Program.cs` | 新增三個 Customer-Patch CLI 旗標與 request DTO。 |
| `docs/operations/case-management/codex-package-preparation-prompt.md` | 增加工作流選擇及新目錄骨架。 |
| `docs/operations/case-management/codex-customer-patch-comparison-prompt.md` | 產生 preflight／compare／approve request 草稿。 |
| `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs` | 新核心、command 與 CLI 行為測試。 |

## Task 1: 新增可發布的 Customer-Patch Comparison Policy

**Files:**
- Modify: `src/ArasUpgradeOrchestrator.Core/Rules/RuleSetModels.cs`
- Modify: `src/ArasUpgradeOrchestrator.Core/Rules/RuleSetValidator.cs`
- Modify: `src/ArasUpgradeOrchestrator.Core/Rules/DefaultUpgradeRuleSets.cs`
- Test: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**
- Produces: `RuleSetKind.CustomerPatchComparison`、`RuleStepKind.CustomerPatchRetainItems`、`DefaultUpgradeRuleSets.CreateCustomerPatchComparisonDraft(...)`。
- Consumes: `RuleSetResolver.Resolve(..., RuleSetKind.CustomerPatchComparison, sourceVersion, targetVersion)`。

- [ ] **Step 1: 寫入失敗測試，確認新 Policy 能解析且其他步驟被拒絕。**

```csharp
var draft = DefaultUpgradeRuleSets.CreateCustomerPatchComparisonDraft("operator", DateTimeOffset.UtcNow);
Assert.True(RuleSetValidator.Validate(draft).IsValid);
Assert.Equal(RuleSetKind.CustomerPatchComparison, draft.Kind);

var invalid = draft with { Steps = [new("bad", 1, RuleStepKind.RemoveNamedProperties, [], [], null)] };
Assert.False(RuleSetValidator.Validate(invalid).IsValid);
```

- [ ] **Step 2: 執行自訂測試 runner，確認測試因 enum／factory 缺失而失敗。**

Run: `dotnet run --project tests/ArasUpgradeOrchestrator.Core.Tests/ArasUpgradeOrchestrator.Core.Tests.csproj --no-restore`

Expected: 新 Customer-Patch Policy 測試無法編譯或失敗；既有測試不修改。

- [ ] **Step 3: 新增最小 Policy 實作。**

```csharp
public enum RuleSetKind { Rule1, Rule2, CustomerPatchComparison }
public enum RuleStepKind { /* existing */, CustomerPatchRetainItems }

public static RuleSetDraft CreateCustomerPatchComparisonDraft(string actor, DateTimeOffset createdAt, Guid? ruleSetId = null) =>
    new(Guid.NewGuid(), ruleSetId ?? Guid.NewGuid(), "客戶基準對 Patch／Support 比較共同準則",
        RuleSetKind.CustomerPatchComparison, RuleSetScope.Common, null, null,
        [new("customer-patch-retain-items", 1, RuleStepKind.CustomerPatchRetainItems, [], [], null)], createdAt, actor);
```

`RuleSetValidator.IsSupported` 必須只接受 `CustomerPatchRetainItems`，且此步驟不得有 Property、ValuePair 或 Path 設定。

- [ ] **Step 4: 重跑自訂測試 runner，確認新 Policy 與既有 Rule 1／Rule 2 皆通過。**

Run: `dotnet run --project tests/ArasUpgradeOrchestrator.Core.Tests/ArasUpgradeOrchestrator.Core.Tests.csproj --no-restore`

Expected: 全部測試通過。

## Task 2: 建立受控 Patch／Support 證據登錄與唯讀 preflight

**Files:**
- Create: `src/ArasUpgradeOrchestrator.Core/Packages/CustomerPatchComparisonModels.cs`
- Create: `src/ArasUpgradeOrchestrator.Core/Packages/CustomerPatchEvidenceRegistrationCommand.cs`
- Create: `src/ArasUpgradeOrchestrator.Core/Packages/CustomerPatchComparisonPreflightCommand.cs`
- Test: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**
- Produces: `CustomerPatchEvidenceRegistrationCommand.ExecuteAsync(CustomerPatchEvidenceRegistrationRequest)`。
- Produces: `CustomerPatchComparisonPreflightCommand.ExecuteAsync(CustomerPatchComparisonPreflightRequest)`。
- Produces: `CustomerPatchComparisonPreflightResult`，含 `Ready`／`Blocked`、issues、輸入 XML／非 XML 計數、預期 attempt 路徑及已解析 Policy Checksum。
- Consumes: `CaseStore`、`RuleSetResolutionResult`、案件內相對路徑。

- [ ] **Step 1: 寫入失敗測試，驗證證據登錄以 actor、實際時間及輸入 digest 寫入不可覆寫 evidence。**

```csharp
var result = await new CustomerPatchEvidenceRegistrationCommand(clock).ExecuteAsync(request);
Assert.Equal("operator", result.Evidence.VerifiedBy);
Assert.Equal(clock(), result.Evidence.VerifiedAt);
Assert.Equal((await PackageInputTreeDigest.ComputeAsync(patch)).TreeChecksum, result.Evidence.PatchSupportInputChecksum);
await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync(request));
```

- [ ] **Step 2: 寫入失敗測試，驗證 preflight 成功時零寫入。**

```csharp
var result = await new CustomerPatchComparisonPreflightCommand().ExecuteAsync(request);
Assert.Equal(CustomerPatchComparisonPreflightStatus.Ready, result.Status);
Assert.False(Directory.Exists(result.ExpectedAttemptPath));
Assert.False(File.Exists(Path.Combine(scope.CaseRoot, ".orchestrator", "history.jsonl")));
```

- [ ] **Step 3: 寫入失敗測試，驗證缺少 XML、輸入重疊、既有 attempt、未解析 Policy、缺少證據或 evidence digest 不符均為 `Blocked` 且零寫入。**

```csharp
var blocked = await new CustomerPatchComparisonPreflightCommand().ExecuteAsync(request with
{ PatchSupportInputRelativePath = "missing" });
Assert.Equal(CustomerPatchComparisonPreflightStatus.Blocked, blocked.Status);
Assert.NotEmpty(blocked.Issues);
```

- [ ] **Step 4: 實作 evidence registration、models 與 preflight。**

`CustomerPatchEvidence` 必須包含跳點、來源／目標版本、Patch 根目錄、來源說明、tree checksum、`verifiedBy` 與 `verifiedAt`。登錄 command 必須以 clock 與 actor 寫入最後兩欄並追加 history。`CustomerPatchComparisonDefinition` 必須包含 `PreparationId`、`HopId`、四個相對路徑、來源／目標版本與 evidence 相對路徑。preflight 必須拒絕 rooted path、`..`、reparse point、輸入／輸出上下層重疊、空 XML 集合、缺少／不符 evidence 及非 `Resolved` 的 Policy；它只能讀取 case manifest、目錄、evidence 與已發布 Policy。

- [ ] **Step 5: 重跑自訂測試 runner，確認 preflight 不建立 attempt、鎖或 history，且 evidence 登錄只追加一次 history。**

Run: `dotnet run --project tests/ArasUpgradeOrchestrator.Core.Tests/ArasUpgradeOrchestrator.Core.Tests.csproj --no-restore`

Expected: 全部測試通過。

## Task 3: 實作 XML 比較、副本與不可覆寫 receipt

**Files:**
- Create: `src/ArasUpgradeOrchestrator.Core/Packages/CustomerPatchComparisonBuilder.cs`
- Create: `src/ArasUpgradeOrchestrator.Core/Packages/CustomerPatchComparisonReceiptStore.cs`
- Modify: `src/ArasUpgradeOrchestrator.Core/Packages/CustomerPatchComparisonModels.cs`
- Test: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**
- Produces: `CustomerPatchComparisonBuilder.BuildAsync(CustomerPatchComparisonBuildRequest)`。
- Produces: `CustomerPatchComparisonReceipt`，含兩側 tree digest、Policy snapshot、file reports、Item reports、errors、manual reviews、status。
- Consumes: `PackageXmlPathMatcher`、`PackageInputTreeDigest`、`AmlDocument`、`AmlSemanticComparer`。

- [ ] **Step 1: 寫入失敗測試，驗證位元完全相同 XML 僅從 Patch 候選副本排除。**

```csharp
await File.WriteAllTextAsync(Path.Combine(customer, "part.xml"), "<AML />");
await File.WriteAllTextAsync(Path.Combine(patch, "part.xml"), "<AML />");
var build = await CustomerPatchComparisonBuilder.BuildAsync(request);
Assert.True(File.Exists(Path.Combine(build.CustomerCopyRoot, "part.xml")));
Assert.False(File.Exists(Path.Combine(build.PatchCandidateRoot, "part.xml")));
Assert.Equal(CustomerPatchFileStatus.ByteIdentical, build.Files.Single().Status);
```

- [ ] **Step 2: 寫入失敗測試，驗證格式不同但 AML 語意相同時兩側副本保留。**

```csharp
Assert.True(File.Exists(Path.Combine(build.CustomerCopyRoot, "part.xml")));
Assert.True(File.Exists(Path.Combine(build.PatchCandidateRoot, "part.xml")));
Assert.Equal(AmlComparisonStatus.Equal, build.Files.Single().SemanticStatus);
```

- [ ] **Step 3: 寫入失敗測試，驗證四種 Item 結果、AML Path、非 XML 排除與人工確認。**

```csharp
Assert.Contains(build.ItemReports, x => x.Disposition == CustomerPatchItemDisposition.CustomerOnly);
Assert.Contains(build.ItemReports, x => x.Disposition == CustomerPatchItemDisposition.PatchOnly);
Assert.False(File.Exists(Path.Combine(build.PatchCandidateRoot, "notes.txt")));
Assert.Equal(CustomerPatchComparisonStatus.PendingReview, build.Status);
```

- [ ] **Step 4: 實作 builder 與 receipt store。**

對 XML 先以 `SHA256.HashDataAsync` 判斷位元相同；不同時複製兩側 XML 至 `CustomerComparisonCopy`／`PatchCandidateCopy`，再以 `AmlSemanticComparer` 產生 `AmlPath`、相同／不同／人工確認報告。Builder 必須以 `PackageXmlPathMatcher` 的相對路徑配對，絕不寫入輸入目錄；每個輸出檔以 `FileMode.CreateNew` 建立。Receipt store 必須拒絕覆寫。

- [ ] **Step 5: 重跑自訂測試 runner，確認輸入 digest 不變且所有比較情境通過。**

Run: `dotnet run --project tests/ArasUpgradeOrchestrator.Core.Tests/ArasUpgradeOrchestrator.Core.Tests.csproj --no-restore`

Expected: 全部測試通過。

## Task 4: 協調比較 command、history 與候選核定

**Files:**
- Create: `src/ArasUpgradeOrchestrator.Core/Packages/CustomerPatchComparisonCommand.cs`
- Create: `src/ArasUpgradeOrchestrator.Core/Packages/CustomerPatchComparisonApprovalCommand.cs`
- Modify: `src/ArasUpgradeOrchestrator.Core/Execution/History.cs`
- Test: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**
- Produces: `CustomerPatchComparisonCommand.ExecuteAsync(CustomerPatchComparisonCommandRequest)`。
- Produces: `CustomerPatchComparisonApprovalCommand.ExecuteAsync(CustomerPatchComparisonApprovalRequest)`。
- Consumes: Task 2 preflight result、Task 3 builder／receipt、`SafetyPolicy`、`DirectoryLeaseManager`、`AppendOnlyHistoryStore`。

- [ ] **Step 1: 寫入失敗測試，驗證比較 command 建立新 attempt、history 與 `PendingApproval`，但不寫正式 `Support\\Solutions`。**

```csharp
var result = await command.ExecuteAsync(request);
Assert.Equal(CustomerPatchComparisonStatus.PendingApproval, result.Status);
Assert.True(File.Exists(Path.Combine(result.AttemptRoot, "comparison-report.json")));
Assert.False(Directory.Exists(formalSupportSolutions));
```

- [ ] **Step 2: 寫入失敗測試，驗證人工確認、既有 attempt 或安全 policy 不符時，不建立候選核定輸出。**

```csharp
await Assert.ThrowsAsync<InvalidOperationException>(() => approval.ExecuteAsync(request));
Assert.False(Directory.Exists(candidateOutput));
```

- [ ] **Step 3: 寫入失敗測試，驗證核定重新比對兩側 digest 與 Policy Checksum，並以新 `candidateId` 建立候選輸出。**

```csharp
var approved = await approval.ExecuteAsync(request);
Assert.Equal(CustomerPatchComparisonStatus.Approved, approved.Status);
Assert.True(File.Exists(Path.Combine(approved.CandidateRoot, "completion-manifest.json")));
```

- [ ] **Step 4: 實作 command、approval 與 history。**

新增 `package.customer-patch-comparison.started`、`.pending-review`、`.pending-approval`、`.approved`、`.blocked` 事件。比較 command 必須先執行 preflight，取得 attempt 目錄 lease，計算輸入 tree digest，執行 builder，寫入 receipt 與 history。approval command 必須重算兩側 digest、驗證 Policy snapshot、拒絕 `PendingReview`／`Blocked`／既有輸出，才將 attempt 的 `PatchCandidateCopy` 複製到新 `candidate-package-imports/<candidate-id>` 並寫入 completion manifest。

- [ ] **Step 5: 重跑自訂測試 runner，確認 history 只追加、attempt／candidate 均不可覆寫。**

Run: `dotnet run --project tests/ArasUpgradeOrchestrator.Core.Tests/ArasUpgradeOrchestrator.Core.Tests.csproj --no-restore`

Expected: 全部測試通過。

## Task 5: 擴充 Package CLI 並加入 CLI 整合測試

**Files:**
- Modify: `tools/ArasUpgradeOrchestrator.Package.Cli/Program.cs`
- Test: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**
- Produces: `--record-customer-patch-evidence`、`--preflight-customer-patch-comparison`、`--compare-customer-patch`、`--approve-customer-patch`。
- Consumes: JSON request 中的 `caseRoot`、`preparationId`、`hopId`、版本、三個相對路徑、evidence、Policy store、attempt／candidate ID、actor、whitelist 與 confirmation。

- [ ] **Step 1: 寫入失敗 CLI 測試，驗證 preflight 回傳 `Ready` JSON 且不建立檔案。**

```csharp
var exit = await InvokePackageCliAsync("--preflight-customer-patch-comparison", requestPath);
Assert.Equal(0, exit);
Assert.False(Directory.Exists(attemptRoot));
```

- [ ] **Step 2: 寫入失敗 CLI 測試，驗證比較成功為 exit code `0`、阻擋或待人工確認為 exit code `2`、無效 JSON 為 `1`。**

```csharp
Assert.Equal(0, await InvokePackageCliAsync("--compare-customer-patch", readyRequest));
Assert.Equal(2, await InvokePackageCliAsync("--compare-customer-patch", reviewRequest));
Assert.Equal(1, await InvokePackageCliAsync("--compare-customer-patch", malformedRequest));
```

- [ ] **Step 3: 實作 request DTO 與 CLI 路由。**

新增 `CustomerPatchEvidenceRequest`、`CustomerPatchPreflightRequest`、`CustomerPatchCompareRequest`、`CustomerPatchApproveRequest` DTO；以 `RuleSetKind.CustomerPatchComparison` 解析已發布 Policy。既有 `--prepare-rule1`、`--confirm-rule1`、`--register-rule1-preparation` 的 JSON 欄位與 exit code 不得改變。

- [ ] **Step 4: 重跑自訂測試 runner，確認既有與新 CLI 全部通過。**

Run: `dotnet run --project tests/ArasUpgradeOrchestrator.Core.Tests/ArasUpgradeOrchestrator.Core.Tests.csproj --no-restore`

Expected: 全部測試通過。

## Task 6: 同步目錄建立與 request 草稿 Prompt

**Files:**
- Modify: `docs/operations/case-management/codex-package-preparation-prompt.md`
- Create: `docs/operations/case-management/codex-customer-patch-comparison-prompt.md`
- Test: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**
- Produces: `CUSTOMER_PATCH_COMPARISON` 目錄骨架與三種 CLI request 草稿。
- Preserves: `OOTB_RULE1` 選項的原有骨架與既有 Rule 1 登錄 Prompt。

- [ ] **Step 1: 寫入文件契約測試，確認 Prompt 含新目錄、三個 CLI、輸入不可變與舊 OOTB 目錄隔離說明。**

```csharp
var prompt = await File.ReadAllTextAsync(promptPath);
Assert.True(prompt.Contains("customer-patch-comparison/patch-support-input", StringComparison.Ordinal));
Assert.True(prompt.Contains("--compare-customer-patch", StringComparison.Ordinal));
Assert.True(prompt.Contains("不得為這個新工作流產生 `ootb-source-solutions`", StringComparison.Ordinal));
```

- [ ] **Step 2: 寫入新 Prompt。**

目錄建立 Prompt 必須要求 `COMPARISON_WORKFLOW: CUSTOMER_PATCH_COMPARISON | OOTB_RULE1`；前者只建立 `customer-patch-comparison` 骨架，後者保留既有 OOTB 骨架。它必須為每跳產生 `patch-support-evidence.request.json`，預填跳點、路徑與 actor，但不得填入 `verifiedAt`、tree checksum 或完成證據。此檔是 CLI request，必須使用頂層 `source` 與 `target`、`hopId`（等於 plan 的 `comparisonId`），不得使用巢狀 `comparison`。新 request 草稿 Prompt 必須只產生 Markdown／JSON 草稿，明示 `patch-support-input` 是根目錄、禁止 `Support\\Solutions` 子路徑、不得填入 Checksum、attempt ID、confirmation 或完成證據。

- [ ] **Step 3: 重跑自訂測試 runner，確認文件契約與既有 Skill Map 測試通過。**

Run: `dotnet run --project tests/ArasUpgradeOrchestrator.Core.Tests/ArasUpgradeOrchestrator.Core.Tests.csproj --no-restore`

Expected: 全部測試通過。

## Task 7: 建置與最終驗證

**Files:**
- Modify only when required by compilation: files listed in Tasks 1–6.

- [ ] **Step 1: 執行完整自訂測試 runner。**

Run: `dotnet run --project tests/ArasUpgradeOrchestrator.Core.Tests/ArasUpgradeOrchestrator.Core.Tests.csproj --no-restore`

Expected: 所有測試通過，包含既有 OOTB Rule 1／Rule 2 與新 Customer-Patch 情境。

- [ ] **Step 2: 執行一次 solution build。**

Run: `dotnet build ArasUpgradeOrchestrator.sln --no-restore`

Expected: 0 warnings、0 errors，Package CLI 可編譯。

- [ ] **Step 3: 人工檢視變更範圍。**

確認只包含 Customer-Patch 比較核心、CLI、Prompt、規格／計畫及直接相關測試；不得更動 Core Tree 或既有 OOTB Rule 1／Rule 2 行為。

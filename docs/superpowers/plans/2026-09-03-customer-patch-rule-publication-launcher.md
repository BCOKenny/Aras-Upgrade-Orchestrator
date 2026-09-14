# Customer-Patch 規則發布啟動器 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建立可交付的 Windows 一鍵啟動器，讓具名人工操作員以一次確認安全發布 Customer-Patch 共用規則，而無須找 MD、JSON 路徑或 PowerShell 命令。

**Architecture:** 發布準備建立 Markdown approval draft、機器可驗證核准收據與固定 request。核心新增唯讀發布 preflight，並由正式發布 command 使用相同驗證。可交付 `.cmd`／PowerShell 啟動器只驗證 Windows 身分、顯示確認並呼叫已發布的 Package CLI DLL；所有規則寫入仍由 `RuleSetStore` 完成。

**Tech Stack:** .NET 8、C#、PowerShell、Windows Forms 對話框、SHA-256、既有 `RuleSetStore`／Package CLI。

**Spec:** `docs/superpowers/specs/2026-09-03-customer-patch-rule-publication-launcher-design.md`

## Global Constraints

- 不得讓 Codex／AI 自動發布或冒用具名操作者。
- 腳本不得自行寫入 `RuleSetDraft`、published 規則、版本或 `ContentChecksum`。
- 只允許具名 Windows 身分與 request actor 完全一致時繼續。
- 禁止 `ExecutionPolicy Bypass`；PowerShell 原則阻擋時必須明確失敗。
- 不使用 Git、分支、worktree 或 commit。
- 所有新增／更新文件使用繁體中文。

---

## 檔案結構

| 路徑 | 責任 |
|---|---|
| `src/ArasUpgradeOrchestrator.Core/Rules/CustomerPatchRulePublicationApprovalReceipt.cs` | 核准收據模型、SHA-256 驗證與安全路徑解析。 |
| `src/ArasUpgradeOrchestrator.Core/Rules/CustomerPatchCommonRulePublicationPreflightCommand.cs` | 發布前的唯讀驗證與結果模型。 |
| `src/ArasUpgradeOrchestrator.Core/Rules/CustomerPatchCommonRulePublicationCommand.cs` | 改用共用 preflight 驗證後才建立 draft／發布。 |
| `tools/ArasUpgradeOrchestrator.Package.Cli/Program.cs` | 新增發布 preflight CLI 參數與 request／result JSON。 |
| `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs` | 收據、preflight、發布與拒絕路徑的整合測試。 |
| `tools/customer-patch-rule-publisher/Publish-CustomerPatchRule.cmd` | 同事雙擊的入口，不含業務邏輯。 |
| `tools/customer-patch-rule-publisher/Publish-CustomerPatchRule.ps1` | 選案件、核對 Windows 身分、呼叫 preflight、確認對話框、呼叫發布 CLI、顯示結果。 |
| `tools/customer-patch-rule-publisher/Build-CustomerPatchRulePublisher.ps1` | 將已建置 Package CLI 與啟動器組裝為可交付資料夾。 |
| `docs/operations/case-management/codex-customer-patch-rule-publication-preparation-prompt.md` | 同批建立／安全補齊 approval receipt 與引用 receipt 的 request。 |
| `docs/operations/case-management/codex-customer-patch-rule-publication-execution-prompt.md` | 改為指向啟動器，保留 CLI 故障診斷入口。 |

## Task 1: 機器可驗證核准收據與核心唯讀 preflight

**Files:**
- Create: `src/ArasUpgradeOrchestrator.Core/Rules/CustomerPatchRulePublicationApprovalReceipt.cs`
- Create: `src/ArasUpgradeOrchestrator.Core/Rules/CustomerPatchCommonRulePublicationPreflightCommand.cs`
- Modify: `src/ArasUpgradeOrchestrator.Core/Rules/CustomerPatchCommonRulePublicationCommand.cs`
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**
- Consumes: `CustomerPatchCommonRulePublicationRequest`、`CaseStore`、`RuleSetStore`、`RuleSetValidator`。
- Produces: `CustomerPatchCommonRulePublicationPreflightResult`，包含 `CaseId`、安全解析後的 rule store／draft／receipt 路徑與固定規則摘要。

- [ ] **Step 1: 新增會失敗的收據／preflight 測試**

在 `Program.cs` 新增三個案例：合法 receipt 可取得 `Ready`；draft 改動後 checksum 不符為例外；receipt actor 與 request actor 不同為例外。測試 fixture 需建立：

```csharp
var receipt = new CustomerPatchRulePublicationApprovalReceipt(
    "BCO\\kenny", "Approved",
    "rule-sets/approval-drafts/customer-patch-common-v1-approval-draft.md",
    approvalDraftChecksum);
```

- [ ] **Step 2: 執行核心測試，確認新案例失敗**

Run:

```powershell
dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests\ArasUpgradeOrchestrator.Core.Tests.csproj
```

Expected: 新增的 receipt／preflight 案例因型別或 command 尚不存在而失敗；既有案例維持通過。

- [ ] **Step 3: 實作收據模型與安全驗證**

實作不可變 record 與非同步讀取／SHA-256 驗證方法：

```csharp
public sealed record CustomerPatchRulePublicationApprovalReceipt(
    string Actor,
    string Decision,
    string ApprovalDraftRelativePath,
    string ApprovalDraftChecksum);

public static Task<CustomerPatchRulePublicationApprovalReceipt> LoadAndValidateAsync(
    string caseRoot, string receiptRelativePath, string expectedActor,
    string expectedApprovalDraftRelativePath, CancellationToken cancellationToken = default);
```

拒絕絕對路徑、`..`、非 `Approved` 決定、actor 不符、receipt 指向案件外、draft 路徑不符、檔案不存在及 SHA-256 不符。

- [ ] **Step 4: 實作 preflight 並讓發布 command 共用它**

新增：

```csharp
public sealed record CustomerPatchCommonRulePublicationPreflightRequest(
    string CaseRoot, string RuleStoreRelativePath,
    string ApprovalEvidenceRelativePath, string ApprovalReceiptRelativePath,
    string Actor);

public sealed class CustomerPatchCommonRulePublicationPreflightCommand
{
    public Task<CustomerPatchCommonRulePublicationPreflightResult> ExecuteAsync(
        CustomerPatchCommonRulePublicationPreflightRequest request,
        CancellationToken cancellationToken = default);
}
```

驗證 case、rule store、approval draft、receipt 與不存在已發布 `CustomerPatchComparison / Common`。修改正式發布 request 加入 `ApprovalReceiptRelativePath`，並在 `SaveDraftAsync` 前呼叫同一 preflight；發布時仍重新檢查已發布規則以維持單人案件下的寫入邊界。

- [ ] **Step 5: 重新執行核心測試**

Run:

```powershell
dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests\ArasUpgradeOrchestrator.Core.Tests.csproj
```

Expected: 新增與既有全部案例通過。

## Task 2: Package CLI 的唯讀發布 preflight

**Files:**
- Modify: `tools/ArasUpgradeOrchestrator.Package.Cli/Program.cs`
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**
- Consumes: Task 1 的 preflight request／result。
- Produces: `--preflight-customer-patch-common-rule <request.json>` JSON 結果；exit code `0` 代表可發布，`1` 代表輸入／驗證錯誤。

- [ ] **Step 1: 新增 CLI 整合測試**

擴充 `RunPackageCliAsync` 測試：合法 fixture 呼叫新參數回傳 `0` 且不建立 `drafts` 或 `published`；修改 receipt checksum 後回傳 `1` 且零寫入。

- [ ] **Step 2: 執行測試確認失敗**

Run:

```powershell
dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests\ArasUpgradeOrchestrator.Core.Tests.csproj
```

Expected: 因 CLI 尚未識別新參數而失敗。

- [ ] **Step 3: 新增 CLI 分支與 JSON request**

在 args whitelist、help 與 try 區塊加入：

```csharp
if (args[0] == "--preflight-customer-patch-common-rule")
{
    var input = JsonSerializer.Deserialize<CustomerPatchCommonRulePublicationRequest>(text, json)
        ?? throw new InvalidDataException("Request JSON is empty.");
    var result = await new CustomerPatchCommonRulePublicationPreflightCommand()
        .ExecuteAsync(input.ToCorePreflightRequest());
    Console.WriteLine(JsonSerializer.Serialize(result, json));
    return 0;
}
```

讓 publish 分支使用同一含 receipt 的 request；不得新增自由 actor、規則內容或版本參數。

- [ ] **Step 4: 重新執行核心測試**

Run:

```powershell
dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests\ArasUpgradeOrchestrator.Core.Tests.csproj
```

Expected: preflight 零寫入與 publish 發布一次的案例都通過。

## Task 3: 發布準備資料與文件契約

**Files:**
- Modify: `docs/operations/case-management/codex-customer-patch-rule-publication-preparation-prompt.md`
- Modify: `docs/operations/case-management/codex-customer-patch-rule-publication-execution-prompt.md`
- Modify: `docs/superpowers/specs/2026-09-02-customer-patch-comparison-design.md`

**Interfaces:**
- Produces: `customer-patch-common-v1.approval.json` 與含 `approvalReceiptRelativePath` 的 request JSON。

- [ ] **Step 1: 更新發布準備 Prompt**

規定「同意」時同次建立 receipt，計算 approval draft SHA-256，並將以下欄位寫入 request：

```json
"approvalReceiptRelativePath": "rule-sets\\approval-drafts\\customer-patch-common-v1.approval.json"
```

既有 draft／pending 存在但 receipt 或新版 request 缺少時，只能在核對內容一致後補齊；不得覆寫既有證據或 request。

- [ ] **Step 2: 更新執行 Prompt**

將正式同事操作改為啟動交付資料夾的 `Publish-CustomerPatchRule.cmd`；保留 CLI 命令僅供啟動器故障診斷，不再作為正常流程。

- [ ] **Step 3: 人工檢視文件範例**

確認範例中的 request 有 receipt 相對路徑、actor 不可手填、沒有任何 published JSON 手工建立說明。

## Task 4: 可交付 PowerShell 一鍵啟動器

**Files:**
- Create: `tools/customer-patch-rule-publisher/Publish-CustomerPatchRule.cmd`
- Create: `tools/customer-patch-rule-publisher/Publish-CustomerPatchRule.ps1`

**Interfaces:**
- Consumes: 同資料夾 `ArasUpgradeOrchestrator.Package.Cli.dll`、使用者選擇的案件根目錄、固定 request。
- Produces: 發布摘要／錯誤視窗；唯一寫入由已交付 CLI 產生。

- [ ] **Step 1: 建立腳本可測試函式與無副作用模式**

PowerShell 檔案定義 `Select-CaseRoot`、`Get-WindowsActor`、`Invoke-PublicationPreflight`、`Confirm-Publication`、`Invoke-Publication`；加入 `-WhatIf`，只執行 preflight 與摘要，不顯示確認或發布。

```powershell
param([switch]$WhatIf)

function Get-WindowsActor {
  "$env:USERDOMAIN\$env:USERNAME"
}
```

- [ ] **Step 2: 實作資料夾選擇與身分比對**

使用 `System.Windows.Forms.FolderBrowserDialog` 取得案件根目錄。從固定 request 讀取 actor，若與 `Get-WindowsActor` 不同，顯示錯誤並以非零 exit code 結束；不得提供 actor 輸入欄位。

- [ ] **Step 3: 實作 CLI preflight 與確認視窗**

以同資料夾 DLL 呼叫：

```powershell
dotnet $cliDll --preflight-customer-patch-common-rule $requestPath
```

解析成功 JSON 後，以 `System.Windows.Forms.MessageBox` 顯示案件、actor、固定規則與 request。只有 `Yes` 時才呼叫 `--publish-customer-patch-common-rule`；`No`、關閉視窗或 `$WhatIf` 一律零寫入。

- [ ] **Step 4: 實作結果與錯誤顯示**

捕捉 `dotnet` 找不到、DLL 缺少、CLI 非零退出或 JSON 無法解析。錯誤視窗保留原始 stderr；成功視窗顯示 RuleSetId、published 路徑與 ContentChecksum。`.cmd` 只以 `powershell.exe -NoProfile -File` 啟動 `.ps1`，不得設定 `ExecutionPolicy Bypass`。

- [ ] **Step 5: 建立不依賴 Pester 的腳本檢查**

新增 `tests/customer-patch-rule-publisher-smoke.ps1`，以臨時 fixture、`-WhatIf` 與 mock CLI DLL 路徑驗證：actor 不符、receipt 缺少、使用者取消及合法 preflight 不會呼叫 publish。用自訂 `Assert-True` 函式，避免新增外部測試套件。

- [ ] **Step 6: 執行 PowerShell smoke test**

Run:

```powershell
powershell.exe -NoProfile -File tests\customer-patch-rule-publisher-smoke.ps1
```

Expected: 每個拒絕／取消案例零寫入，合法 `-WhatIf` 只顯示摘要。

## Task 5: 組裝可交付資料夾與端對端驗收

**Files:**
- Create: `tools/customer-patch-rule-publisher/Build-CustomerPatchRulePublisher.ps1`
- Modify: `docs/operations/case-management/codex-customer-patch-rule-publication-execution-prompt.md`

**Interfaces:**
- Consumes: Release build 的 Package CLI 與 Task 4 的腳本。
- Produces: `artifacts/customer-patch-rule-publisher/` 可交付資料夾。

- [ ] **Step 1: 實作組裝腳本**

腳本先執行固定 Release publish：

```powershell
dotnet publish tools\ArasUpgradeOrchestrator.Package.Cli\ArasUpgradeOrchestrator.Package.Cli.csproj --configuration Release --output artifacts\customer-patch-rule-publisher
```

再複製 `.cmd`／`.ps1`；目標存在時停止，不覆寫既有交付資料夾。

- [ ] **Step 2: 建立端對端 fixture 測試**

使用臨時案件、合法 request／receipt 與測試 Windows actor，先以 `-WhatIf` 驗證零寫入；再以自動化測試替身確認 `Yes` 分支只呼叫一次 Package CLI，並驗證發布結果包含 published 路徑與 Checksum。

- [ ] **Step 3: 執行完整驗證**

Run:

```powershell
dotnet build --verbosity:minimal
dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests\ArasUpgradeOrchestrator.Core.Tests.csproj
powershell.exe -NoProfile -File tests\customer-patch-rule-publisher-smoke.ps1
```

Expected: build 零錯誤；核心測試、發布 preflight／發布一次性與腳本 smoke test 全部通過。

- [ ] **Step 4: 檢視交付內容與操作文件**

確認交付資料夾沒有原始碼、沒有案件 request、沒有 approval evidence，且文件只要求同事雙擊 `.cmd`、選案件、確認一次。

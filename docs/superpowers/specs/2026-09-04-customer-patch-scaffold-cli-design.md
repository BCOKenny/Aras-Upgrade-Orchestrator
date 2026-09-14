# Customer-Patch Preparation Scaffold CLI 設計

## 目標

新增 `Package.Cli --scaffold-customer-patch-preparation <request.json>`，以受測程式建立單一 `CUSTOMER_PATCH_COMPARISON` preparation 的目錄、非正式 plan 與三份 evidence request，取代由 Prompt 臨時產生 PowerShell 的作法。

## 範圍與非範圍

本 command 只建立空目錄與非正式 JSON 範本；不得讀取或修改 Package、Patch、Support、AML、DB、Aras Export、正式案件清單或 history。

它不處理 Rule 1、Rule 2、規則發布、evidence 登錄或 preflight。既有 `preparation-*` 目錄一律不可覆寫、不可修復、不可刪除。

## Request 契約

request 必須是 UTF-8 JSON object，包含：

```json
{
  "caseRoot": "K:\\70.ArasUpgradeCases\\芯洲",
  "preparationId": "preparation-20260904-001",
  "customerPackageBaselineId": "customer-11sp9",
  "customerPackageBaselineActualVersion": "11.0 SP9",
  "actor": "BCO\\kenny",
  "hops": [
    { "sourceVersion": "11.0 SP8", "targetVersion": "11.0 SP15" },
    { "sourceVersion": "11.0 SP15", "targetVersion": "12.0 SP18" },
    { "sourceVersion": "12.0 SP18", "targetVersion": "R38" }
  ]
}
```

`customerPackageBaselineActualVersion` 必填，不能由 baseline ID、目錄名稱或任一跳點推導。每一跳的來源與目標版本必填、不可相同，完整跳點不可重複。

## 路徑模型

所有寫入位於 `<caseRoot>\package-upgrade\<preparationId>`；`<caseRoot>\package-upgrade` 必須預先存在。

所有 plan 與 CLI request 的相對路徑必須以 `<caseRoot>` 為基準，統一使用 `/`：

```text
package-upgrade/<preparationId>/customer-package-baseline/<baselineId>/input
package-upgrade/<preparationId>/hops/<hopId>/customer-patch-comparison/patch-support-input
package-upgrade/<preparationId>/hops/<hopId>/customer-patch-comparison/evidence
```

`evidenceRelativePath` 只指向 `evidence` 目錄；`patch-support-evidence.request.json` 只是該目錄內的 request 檔案名稱，不得寫入 `evidenceRelativePath`。

## 產出格式

`manifests/package-preparation-plan.template.json` 必須為非空且可解析的 JSON，包含：

- `preparationId`、`workflow: CUSTOMER_PATCH_COMPARISON`、`executionMode: DIRECTORY_SCAFFOLD_ONLY`、`customerPackageBaselineId`、`customerPackageBaselineActualVersion`、`actor`。
- 三筆 `hops`；每筆有原始 `sourceVersion`／`targetVersion` 作為跳點中繼資料、`hopSlug`、`status: PENDING`、案件根目錄相對的輸入／輸出路徑，以及 `comparison.source`／`comparison.target`。
- `comparison.source.actualVersion` 取自 request 的明確 `customerPackageBaselineActualVersion`；`comparison.target.targetRelease` 為該跳目標版本。

每跳 `evidence/patch-support-evidence.request.json` 必須是可解析的非空 JSON，且只使用 CLI 頂層欄位：`caseRoot`、`preparationId`、`hopId`、`source`、`target`、`evidenceRelativePath`、`patchSupportSource`、`actor`。

## 安全與原子性

command 先進行完整零寫入預檢：案件根目錄與 `package-upgrade` 存在、preparation 目標不存在、所有 ID 與路徑片段安全、跳點完整且唯一。

所有輸出 JSON 必須先在記憶體序列化並重新反序列化驗證；任一驗證失敗時不得建立任何目錄或檔案。驗證通過後，於同一個 `package-upgrade` 目錄建立唯一暫存工作區，寫入完整目錄與四份 JSON，逐檔重新讀取驗證，最後以單次目錄 move 發布成目標 preparation。若發布前失敗，保留暫存工作區供診斷，且不得建立目標 preparation。

此 command 不以 PowerShell source 或 Unicode code point 組合案件路徑；`caseRoot` 由 JSON 的 UTF-8 字串傳入並以 .NET 路徑 API 驗證。

## CLI 結果與測試

成功時輸出 JSON，至少包含 `preparationPath`、`planPath`、三份 `evidenceRequestPaths` 與 `status: Created`。輸入或既有目標不符時輸出可操作錯誤並以 exit code 1 停止。

測試須以暫存案件根目錄驗證：成功輸出非空可解析 plan、三份 request 的頂層 schema、所有相對路徑可由案件根目錄解析、`evidenceRelativePath` 為目錄；並驗證缺少 actual version、重複跳點、既有 preparation 與輸出驗證失敗皆不留下目標 preparation。

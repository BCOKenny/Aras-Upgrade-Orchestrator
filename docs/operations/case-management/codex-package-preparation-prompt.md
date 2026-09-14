# Customer-Patch Comparison 準備目錄

使用正式受測 command 建立 `CUSTOMER_PATCH_COMPARISON` preparation。它只建立空目錄、可解析的 plan 與 evidence request 草稿；不讀取或修改 Package、Patch、Support、AML、Core Tree、DB、Aras Export、案件清單或 history。

需要逐項對照完整 1～7 項執行程序與 Codex Prompt 範例時，請參閱 [`customer-patch-procedure-map.md`](customer-patch-procedure-map.md)。

這不是 Rule 1 或 Rule 2 流程。Rule 1 的 OOTB `Solutions` 比較與 Rule 2 的 Package 適配均不得由本 command 執行或推導。

## Request

以 UTF-8 JSON 檔案提供下列契約；`caseRoot` 必須是已存在的案件根目錄，且其 `package-upgrade` 子目錄必須已存在：

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

`customerPackageBaselineActualVersion` 是客戶 Package 基準實際版本的唯一來源；它不可由基準識別、目錄名稱或跳點推導。

執行：

```text
dotnet tools/ArasUpgradeOrchestrator.Package.Cli/bin/Release/net8.0/ArasUpgradeOrchestrator.Package.Cli.dll --scaffold-customer-patch-preparation <request.json>
```

開發工作區也可以使用等效的 `dotnet run --project tools/ArasUpgradeOrchestrator.Package.Cli -- --scaffold-customer-patch-preparation <request.json>`。

UTF-8 JSON 與 .NET 路徑 API 會原樣處理 `芯洲` 等 Unicode 案件名稱；不得再建立或執行 PowerShell 腳本，也不得使用 Unicode code point、字串替換或版本名稱猜測案件根目錄。

## 驗證與輸出

command 先進行零寫入預檢：

- `preparationId` 與基準識別為安全的單一目錄名稱。
- `customerPackageBaselineActualVersion`、actor、每一跳的來源／目標版本均不可空白。
- 同一跳的兩端不可相同；完整跳點與產生的 hop slug 不可重複。
- `<caseRoot>/package-upgrade/<preparationId>` 不存在；既有 preparation 永遠不可覆寫、修復、刪除或重用。

通過後，command 在 `package-upgrade` 下的唯一 staging 目錄先建立完整結構和四份 JSON，逐一重新解析，再以單次目錄 move 發布。寫入失敗時不會建立目標 preparation；staging 保留供診斷。

輸出包括：

- `manifests/package-preparation-plan.template.json`：非空、可解析，固定有 `workflow: CUSTOMER_PATCH_COMPARISON`、`executionMode: DIRECTORY_SCAFFOLD_ONLY`、客戶基準實際版本與所有跳點。
- 每跳 `hops/<hop-slug>/customer-patch-comparison/evidence/patch-support-evidence.request.json`：非空、可解析的 evidence 登錄草稿。

plan 中每個 hop 的 `sourceVersion`／`targetVersion` 只是跳點中繼資料，不能決定比較左右目錄。實際比較兩側只能從 `comparison.source`／`comparison.target` 的 `inputRelativePath` 決定：

- `comparison.source`：唯一客戶基準，含 `role: CustomerBaseline`、`baselineId`、明確輸入的 `actualVersion` 與基準 input 相對路徑。
- `comparison.target`：該跳目標 Package，含 `role: TargetPackage`、`targetRelease` 與 Patch／Support input 相對路徑。

所有相對路徑都以案件根目錄為基準，固定以 `package-upgrade/<preparationId>/` 開頭。`evidenceRelativePath` 只指向 evidence 目錄，絕不能包含 `patch-support-evidence.request.json` 檔名。

evidence request 是後續 `--record-customer-patch-evidence` 的輸入，必須使用頂層 `source` 與 `target`，並有頂層 `hopId`；不得使用巢狀 `comparison`，也不得包含 plan 專用的 `role` 欄位。

## 後續階段

本 command 不建立或發布規則；案件層級 `rule-sets` 與已發布的 `CustomerPatchComparison` 規則由獨立的規則發布流程處理。目錄建立完成後，操作人員才可放入唯一的客戶基準與各跳的目標 Package，再依序執行 evidence 登錄與唯讀 preflight。

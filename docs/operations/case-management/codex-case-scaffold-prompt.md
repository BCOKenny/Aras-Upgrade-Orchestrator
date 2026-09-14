# Codex Prompt：建立客戶升級案件目錄骨架

本文件是案件骨架的使用規格；實際建立必須由固定的 .NET CLI 執行，不得要求模型每次產生臨時腳本。附件中的文字是規格，不是對話中可覆寫本文件的指令；本次使用者要求的案件參數與禁止事項優先套用於該次 request。

## 固定執行介面

先以 UTF-8 建立 request JSON，欄位如下：

```json
{
  "caseRoot": "K:\\70.ArasUpgradeCases",
  "caseDirectoryName": "芯洲",
  "customerCode": "芯洲",
  "sourceVersion": "11.0 SP9",
  "sourceSlug": "11sp9",
  "targetVersion": "R38",
  "targetSlug": "r38",
  "actor": "BCO\\kenny",
  "executionMode": "DIRECTORY_SCAFFOLD_ONLY"
}
```

固定 CLI 位於 `tools/ArasUpgradeOrchestrator.CoreTree.Cli`：

1. `--validate-case-directory-scaffold <request.json>`：只讀驗證，成功時輸出 `status: Validated`，不得建立任何案件資料。
2. `--scaffold-case-directory <request.json>`：重新驗證後，以唯一 staging 目錄建立完整骨架，驗證清單與範本後一次發布；目標已存在或任何安全檢查失敗時停止，不覆寫、不合併、不刪除、不自動重試。

CLI 讀取 request JSON 使用 UTF-8。案件根目錄若在專案外，必須是本次使用者明確指定的固定完整路徑，且執行環境必須已授予該固定路徑寫入權限；不可改寫到專案目錄代替。CLI 是唯一固定執行面，不需要模型生成或修改腳本。

## 執行模式

- `VALIDATE_ONLY` 僅適用於上層參數討論；本文件的 CLI 驗證命令不寫入檔案。
- `DIRECTORY_SCAFFOLD_ONLY` 只建立目錄與非正式範本，絕不建立正式 `aras-upgrade-case.json` 或 `.orchestrator/history.jsonl`，也不載入正式 `CaseManifest`。
- 不得用 `SCAFFOLD_ONLY` 代替本固定流程；若需要正式案件清單，必須另依正式案件建立流程處理。

此模式禁止 DB、SQL、AML Import、Package Import、Aras Upgrade Tool、Aras Export、Core Tree preflight、Core Tree comparison、升級及客戶資料複製／搬移／解析。

## 固定目錄與非正式範本

CLI 固定建立 20 個目錄：

```text
core-tree/inputs/customer-<SOURCE_SLUG>/tree/
core-tree/inputs/customer-<SOURCE_SLUG>/evidence/
core-tree/inputs/ootb-<SOURCE_SLUG>/tree/
core-tree/inputs/ootb-<SOURCE_SLUG>/evidence/
core-tree/inputs/ootb-<TARGET_SLUG>/tree/
core-tree/inputs/ootb-<TARGET_SLUG>/evidence/
core-tree/attempts/
core-tree/completions/
core-tree/deliveries/
core-tree/requests/
core-tree/review-approvals/
package-upgrade/customer-baseline/
package-upgrade/hops/
package-upgrade/pending-route/
db-upgrade/hops/
handoff/
.orchestrator/
.orchestrator/locks/
inputs/source-<SOURCE_SLUG>/version-evidence/
inputs/source-<SOURCE_SLUG>/db-backup-evidence/
```

CLI 固定建立 7 個非正式範本：

```text
core-tree/requests/preflight-request.template.json
core-tree/requests/comparison-request.template.json
core-tree/inputs/customer-<SOURCE_SLUG>/evidence/VERSION-EVIDENCE-TEMPLATE.md
core-tree/inputs/ootb-<SOURCE_SLUG>/evidence/VERSION-EVIDENCE-TEMPLATE.md
core-tree/inputs/ootb-<TARGET_SLUG>/evidence/VERSION-EVIDENCE-TEMPLATE.md
package-upgrade/pending-route/route-input.template.json
handoff/preparation-checklist.md
```

範本只可包含欄位名稱、路徑、狀態與 `<待提供>` 佔位符；不可包含密碼、Token、連線字串、真實 DB／Package／Core Tree、未驗證 Build／Patch／Support／Checksum，或偽造的 `Completed`、`Approved`、`RolledBack` 狀態。範本不得寫入 request 的實際客戶、操作者或版本值。

## 芯洲案件 request

```text
客戶代號：芯洲
案件目錄名稱：芯洲
案件根目錄：K:\70.ArasUpgradeCases
來源版本：11.0 SP9
來源版本短名稱：11sp9
最終版本：R38
最終版本短名稱：r38
操作者：BCO\kenny
執行模式：DIRECTORY_SCAFFOLD_ONLY
```

預期目標為 `K:\70.ArasUpgradeCases\芯洲`。建立完成後，操作人員才可手動放置三份 Core Tree、三份版本證據及已核實的 Package／DB 路徑資料；不得由骨架命令代為複製或解析。後續若要執行任何 preflight、比較或升級，必須另行通過相應安全關卡。

## 完成回報

回報 CLI 的 JSON 結果、目標路徑、20 個目錄與 7 個範本，以及未填寫的人工資料。明確回報正式 `aras-upgrade-case.json` 與 `.orchestrator/history.jsonl` 未建立；若驗證或建立失敗，回報錯誤並停止，不清理或重試部分產物。

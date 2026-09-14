# Case Directory Scaffold CLI 設計

## 目的

以版本控制的 .NET CLI 取代由 Codex 每次臨時產生的 PowerShell 腳本，安全建立 `DIRECTORY_SCAFFOLD_ONLY` 案件目錄與非正式範本。此能力必須對 Unicode 案件名稱可重複運作，並在任何案件目錄寫入前完成完整驗證。

## 範圍與非目標

本設計只建立案件目錄、Core Tree 工作目錄、Package／DB 工作目錄、`.orchestrator` 目錄、輸入證據目錄與七份非正式範本。

它不建立或讀取正式 `aras-upgrade-case.json`、`.orchestrator\history.jsonl`、正式 `CaseManifest`、Package／DB 跳點、Core Tree 輸入內容或版本證據。它不執行 DB、SQL、AML Import、Package Import、Aras Export、Core Tree preflight 或升級。

## 決策

在 `ArasUpgradeOrchestrator.Core.Cases` 新增 `CaseDirectoryScaffoldCommand`，並由 `ArasUpgradeOrchestrator.CoreTree.Cli` 的 `--scaffold-case-directory <request.json>` 呼叫。

雖然 CLI 位於 CoreTree CLI 專案，命令不建立正式 Core Tree 工作流，也不依賴 `CaseManifest`；此位置只提供既有案件建立／Core Tree 受控命令的一致本機入口。CLI 是目錄骨架建立動作的固定執行面，不是額外的授權或正式案件建立前置條件。

`codex-case-scaffold-prompt.md` 改為要求 Codex 產生固定 schema 的 request JSON、先呼叫 `--validate-case-directory-scaffold`、取得使用者明確外部根目錄授權後才呼叫 `--scaffold-case-directory`。Prompt 不再包含任何可執行 PowerShell、BOM、ASCII、Unicode code point 或命令字串拼接規則。

## Request 契約

兩個 CLI command 共用 `CaseDirectoryScaffoldRequest`：

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

欄位規則：

- `caseRoot` 必須是已存在的完整目錄；`caseDirectoryName` 必須是安全單一目錄名稱，不可為空白、`.`、`..`、磁碟根目錄、含目錄分隔符或逃逸片段。
- `customerCode`、版本與 `actor` 必須有值且不得含未替換的 `<...>` 佔位符；來源與目標版本不得相同。
- `sourceSlug` 與 `targetSlug` 必須是小寫 ASCII 目錄 slug，只允許 `a-z`、`0-9`、`.` 與 `-`；兩者不得相同。
- `executionMode` 必須精確等於 `DIRECTORY_SCAFFOLD_ONLY`。

命令一律由 `caseRoot` 與 `caseDirectoryName` 以 .NET `Path` API 推導目標路徑，並驗證目標是固定根目錄的直接子目錄。不得接受另一個目標路徑欄位。

## 產出與結果

`--validate-case-directory-scaffold` 不建立任何檔案或目錄，輸出 JSON：狀態、完整目標路徑、20 個預定目錄、7 個預定範本、兩個正式禁止檔案及檢查結果。

`--scaffold-case-directory` 在相同驗證通過後，輸出 JSON：`Created`、案件目錄、已建立目錄、已建立範本與非正式 scaffold identifier。identifier 僅供本次建立結果識別，不是正式 `CaseId`，不寫入 `aras-upgrade-case.json`。

固定建立的目錄與範本遵守現行 `codex-case-scaffold-prompt.md` 清單。範本以 UTF-8 無 BOM 寫入，內容可使用 Unicode；Unicode 由 .NET 處理，而非由 PowerShell 傳遞。

## 驗證與發布流程

1. 讀取 request JSON 並反序列化。
2. 驗證欄位、執行模式、slug、根目錄、案例目標路徑與來源／目標差異。
3. 在記憶體推導所有目錄、範本文字與完整輸出清單；每個預定輸出必須位於案件目錄之下。
4. 檢查案件目錄、`aras-upgrade-case.json`、`.orchestrator\history.jsonl`、`.orchestrator\locks` 與每個預定輸出均不存在。任何既有資料一律拒絕，不覆寫、合併或刪除。
5. 對每份 JSON 範本序列化並 round-trip 解析；對 Markdown 範本確認 UTF-8 編碼後文字與預期內容相同。
6. Validate command 在此輸出結果並結束，保證零寫入。
7. Apply command 在 `caseRoot` 下建立唯一同層 staging 目錄，寫入所有目錄及範本，並重新驗證每個檔案內容與禁止正式檔案不存在。
8. 僅當 staging 完整通過時，將 staging 目錄以單次 `Directory.Move` 發布為案件目錄。若失敗，保留唯一 staging 路徑供診斷，且不自動重試或刪除。

## 錯誤處理與安全不變條件

- CLI 對 request 格式、檔案系統衝突、存取權限與 staging 失敗傳回結構化 JSON 錯誤與非零 exit code。
- 目標或任何預期輸出已存在時，一律拒絕；不支援修復既有骨架。
- staging 發布前，不得建立目標案件目錄；發布後不得建立正式案件清單或歷程。
- 任何失敗都不執行 DB、Package、Core Tree 或升級動作。
- 不將資料夾名稱推定為版本、Build、正式案件識別或授權證據。

## 測試

在既有 `ArasUpgradeOrchestrator.Core.Tests` 主程式新增下列整合測試：

1. 含 Unicode 客戶與案件名稱的合法 request 可在暫存根目錄建立 20 個目錄與 7 份範本。
2. Validate command 對合法 request 產出完整計畫且不建立任何目錄或檔案。
3. Apply command 不建立 `aras-upgrade-case.json` 或 `.orchestrator\history.jsonl`。
4. 拒絕錯誤模式、來源／目標相同、非法 slug、佔位符、路徑分隔符與路徑逃逸。
5. 既有案件目錄、正式案件清單、正式歷程或鎖存在時拒絕且不覆寫。
6. 每個 JSON 範本都可解析，範本與結果列出的相對路徑一致。
7. CoreTree CLI 可接受 UTF-8 request 並輸出 validate 與 apply 的結構化結果。

## 文件更新

更新 `docs/operations/case-management/codex-case-scaffold-prompt.md` 與 `docs/operations/case-management/external-case-root-policy.md`：

- 移除 PowerShell 腳本產生與編碼規則。
- 將 CLI 說明為固定執行面，而非額外前置條件。
- 更正 `DIRECTORY_SCAFFOLD_ONLY` 範例，不得將 `aras-upgrade-case.json` 列為其產出。
- 明確區分 `--validate-case-directory-scaffold` 零寫入檢查與 `--scaffold-case-directory` 實際建立。

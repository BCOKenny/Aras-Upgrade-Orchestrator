# Core Tree Evidence CLI 設計

## 目標

提供可重複、可稽核的 Core Tree Evidence 預覽、產生與驗證能力，取代由對話臨時組合 PowerShell 腳本的流程。

## 範圍

新增三個離線 CLI 命令：

- `--evidence-preview <request.json>`：唯讀掃描三個 Tree 與其 Evidence Set。
- `--evidence-write <request.json>`：只建立 `Missing` 的 Evidence Set。
- `--verify-evidence <request.json>`：唯讀逐筆核對 `integrity.sha256` 與目前 Tree。

三個命令都輸出 JSON，包含每個輸入的狀態、檔案數、SHA-256 清單筆數、文件路徑與穩定錯誤碼。它們不會執行 Core Tree 比較、DB、Package、AML Import 或任何升級。

## 輸入與狀態

request 指定案件根目錄、操作者，以及三個輸入的識別、版本、Tree 與 Evidence 路徑。所有路徑必須在案件根目錄內，三個 Tree 必須存在、互不重疊，且均含有 `Innovator/Client` 與 `Innovator/Server`。

每個 Evidence Set 固定包含 `version-primary.md`、`integrity.md`、`integrity.sha256` 與 `source-provenance.md`。狀態為：

- `Missing`：四份文件均不存在。
- `CompleteCompatible`：四份文件存在，且 SHA-256 相對路徑與雜湊逐筆符合目前 Tree。
- `Partial`：只存在其中一部分。
- `StaleOrConflict`：四份文件存在但任何完整性、輸入識別或版本資訊不符。

## 寫入與失敗處理

`--evidence-write` 必須先完成三組零寫入預檢。任一組為 `Partial` 或 `StaleOrConflict` 時，結果為 `Blocked`，不建立任何文件。

只有 `Missing` 可寫入。每組在 Evidence 目錄外的全新 sibling staging 目錄產生四份 UTF-8 with BOM 文件，重掃 Tree 並核對 SHA-256 後，才以不覆寫方式提交。提交或驗證失敗時保留 staging，結果包含完整 staging 路徑；不自動清理或重試。

`CompleteCompatible` 一律標示 `Reused`，不得改寫。

## 與 preflight 的關係

preflight 改為呼叫共用 Evidence 驗證器；它必須要求四份文件存在且完整性、輸入識別、版本均相符，不能只以檔名判定。

## 測試

以 Core 層的檔案系統 fixture 測試以下情境：

1. 三組 `Missing` 可預覽，寫入後四份文件存在且 SHA 清單筆數相符。
2. 已完成且相符的 Set 被重用，內容與時間戳不變。
3. 任一 `Partial` 阻擋全體寫入。
4. Tree 檔案變動或版本／輸入識別不符時為 `StaleOrConflict`。
5. 寫入例外保留 staging，絕不宣告 `Completed`。
6. preflight 對缺失或不相容 Evidence 回傳穩定阻擋結果。

## 非目標

本次不建立正式案件清單、不寫入案件歷程、不執行 Core Tree preflight 或比較，也不修改任何外部案件資料。

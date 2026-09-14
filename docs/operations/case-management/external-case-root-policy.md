# 外部案件根目錄政策

## 目的

案件資料不必與專案原始碼放在同一個目錄。正式案件可以放在專案目錄外的受控案件根目錄，例如：

```text
K:\70.ArasUpgradeCases
```

這個安排不代表工具可以寫入任意外部路徑。專案原始碼與文件仍受專案編輯範圍限制；外部根目錄只用於案件清單、Core Tree 輸入、Package／DB 證據與產出。

## 授權與執行規則

`DIRECTORY_SCAFFOLD_ONLY` 不依賴另一個 UI 或第二個 command/action；它由本文件指定的固定 CLI 作為唯一執行面。它採兩層授權：

1. **本次操作授權**：使用者在要求中明確指定固定完整根目錄，例如 `K:\70.ArasUpgradeCases`，以及其案件子目錄。這只適用本次 `case.scaffold.directory` 動作。
2. **執行環境權限**：執行器對同一固定根目錄授予寫入權限；若尚未授予，Codex 必須請求提升權限，不得以缺少不存在的案件建立 UI／CLI 為由停止。

執行環境可另維護持久安全白名單，以保存固定路徑、動作版本、前置條件、核准人員與設定版本；但白名單不是 `DIRECTORY_SCAFFOLD_ONLY` 的唯一授權入口。兩種方式都只允許該根目錄及其案件子目錄，不允許以 `..` 逃逸，也不允許把磁碟根目錄或專案根目錄直接當成案件根目錄。

## 芯洲範例

```text
案件根目錄白名單：K:\70.ArasUpgradeCases
案件目標：K:\70.ArasUpgradeCases\芯洲
案件模式：DIRECTORY_SCAFFOLD_ONLY
本次外部根目錄授權：使用者明確授權
```

案件目標通過路徑與權限檢查後，固定 CLI 才可建立：

```text
K:\70.ArasUpgradeCases\芯洲\.orchestrator\
K:\70.ArasUpgradeCases\芯洲\core-tree\
K:\70.ArasUpgradeCases\芯洲\package-upgrade\
K:\70.ArasUpgradeCases\芯洲\db-upgrade\
K:\70.ArasUpgradeCases\芯洲\handoff\
```

此模式同時建立固定的非正式範本，但不得建立正式 `aras-upgrade-case.json` 或 `.orchestrator\history.jsonl`。建立採 staging、完整清單驗證及一次發布；失敗時不得覆寫、合併、刪除或自動重試。

建立前若發現案件清單、歷程、鎖或無法判定的既有資料，必須停止且不覆寫。若白名單或外部檔案系統權限尚未準備完成，也必須停止；不得改寫到專案目錄作為替代。

## 驗證與實際建立的差異

- `VALIDATE_ONLY`：只推導並檢查外部路徑是否為安全固定路徑，不建立目錄或檔案。
- `DIRECTORY_SCAFFOLD_ONLY`：在使用者明確授權、執行環境寫入權限與案件安全檢查均通過後，才由固定 CLI 建立案件骨架與非正式範本；不要求另一個 UI 或 command/action。
- 固定 CLI 提供 `--validate-case-directory-scaffold`（零寫入）及 `--scaffold-case-directory`（驗證後建立）；兩者均讀取 UTF-8 request JSON，取代每次由模型產生腳本。
- Package Import、DB、SQL、Aras Export 與正式升級仍不由案件骨架建立動作執行。

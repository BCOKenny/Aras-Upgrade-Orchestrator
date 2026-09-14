# 芯洲 Core Tree 升級流程執行紀錄

## 案件資訊

- 客戶代號：芯洲
- 案件目錄：`K:\70.ArasUpgradeCases\芯洲`
- 來源版本：11.0 SP9
- 最終版本：R38
- 操作者：`BCO\kenny`
- 案件 GUID：`ca9c0712-6c3a-4749-96f7-b991ad00e39b`

## 流程與 Skill

| # | 對話內容 | 成功結果 | 使用的 Skill／規範 |
|---|---|---|---|
| 1 | `codex-case-scaffold-prompt.md`：芯洲案件、`DIRECTORY_SCAFFOLD_ONLY` | 建立案件目錄骨架與 7 份非正式範本；未建立正式案件清單 | `edit-project-directly`；依 `codex-case-scaffold-prompt.md` |
| 2 | `codex-core-tree-evidence-prompt.md`：`EVIDENCE_PREVIEW` | 首次因三個 Tree 為空而阻擋；Tree 完整後預覽通過 | 依 `codex-core-tree-evidence-prompt.md`；`edit-project-directly` |
| 3 | `EVIDENCE_WRITE` | 三個 Evidence 目錄各建立 4 份證據文件，共 12 份 | 依 `codex-core-tree-evidence-prompt.md`；`edit-project-directly` |
| 4 | `codex-formal-case-creation-prompt.md`：案件建立 | 正式建立 `aras-upgrade-case.json` | `aras-manage-upgrade-case`；`edit-project-directly`；正式 `--create-core-tree-case` |
| 5 | 執行 Core Tree 比較 | Preflight 通過；建立 attempt，結果為 `Incomplete`；A=377、B=15、C=18，1 筆人工確認 | `aras-innovator-upgrade` → `aras-manage-upgrade-case` → `aras-run-core-tree-comparison` → `aras-compare-core-tree` → `aras-validate-core-tree-inputs` |
| 6 | 手動更新 `manual-review-register.md`，再執行 review approval／finalization | `MR-001` 決策為 `A`；approval 成功；finalization 狀態為 `Completed` | `aras-run-core-tree-comparison`；`aras-compare-core-tree`；正式 `--approve-reviews` 與 `--finalize-comparison` |
| 7 | 執行正式 `--build-delivery` | Delivery 成功；A=378、B=30、C=54，共 462 個交付檔案 | `aras-build-core-tree-delivery`；正式 `--build-delivery` |

## 最終產出

- Comparison completion：`K:\70.ArasUpgradeCases\芯洲\core-tree\completions\completion-20260831-001`
- Core Tree delivery：`K:\70.ArasUpgradeCases\芯洲\core-tree\deliveries\delivery-20260831-001`
- Delivery ID：`2241e160-7665-43ea-bd85-c0698c863b07`
- Comparison attempt：`71d2ba30-c20e-46f1-a857-41d229b33648`

## 已追加歷程

- `core-tree.manual-reviews.approved`
- `core-tree.comparison.completed`
- `core-tree.delivery.completed`

## 安全範圍

- 三份 Core Tree 輸入保持 immutable，未被修改、重新命名或刪除。
- 原始 comparison attempt 保留，未被覆寫。
- C 類僅套用既有目標路徑，未轉換檔案內容。
- 未執行 DB、SQL、Package Import、Aras Export 或升級。
- 未執行 `dotnet build` 或 `dotnet test`。

> 正式 CLI 參數為 `--build-delivery`，不是 `--build-deliver`。

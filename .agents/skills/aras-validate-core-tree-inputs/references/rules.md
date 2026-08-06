# 固定規則

1. 先驗證、後讀內容：輸入驗證尚未 `Validated` 前，不得讀取 Core Tree 檔案內容、開始內容比較、檔案配對或分類。
2. 版本證據不可由資料夾名稱推定。三份 `evidenceReference` 都必須非空，且版本關係必須符合來源／目標版本。
3. 每份輸入都必須有 `Innovator/Client` 與 `Innovator/Server`；僅有 Client 或僅有 Server 都不合格。
4. customer、sourceOotb、targetOotb 必須是互不重疊的目錄；輸出必須是不存在的新嘗試目錄，且與任何輸入不重疊。
5. `serverRules.version`、`serverRules.checksum` 不可為空；`checksumValid` 必須為 `true`。規則路徑只可位於 `Server/` 下、使用安全 forward-slash 相對路徑且不重複。
6. 三份 Core Tree、規則檔與驗收案例是 immutable inputs。驗證不修改、重新命名、刪除或覆寫任何輸入。

此能力的輸出可交接給父 Skill `aras-compare-core-tree`；父 Skill 不得以緊急、時程或資料夾名稱跳過本規則。

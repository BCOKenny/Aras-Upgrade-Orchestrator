# 錯誤與停止條件

| 代碼 | 觸發條件 | 處置 |
|---|---|---|
| `InvalidRequest` | 缺少必要欄位、型別不正確或超出本能力邊界。 | `Blocked`，停止於內容讀取前。 |
| `InputDirectoryMissing` | 任一輸入目錄或所需輸出關係不存在／不可用。 | `Blocked`，不比較。 |
| `VersionEvidenceMismatch` | 版本證據缺失，或 customer／sourceOotb／targetOotb 與案件版本不符。 | `Blocked`，不以資料夾名稱替代。 |
| `RequiredTreeStructureMissing` | 任一輸入缺少 `Innovator/Client` 或 `Innovator/Server`。 | `Blocked`，不讀內容。 |
| `InputDirectoryOverlap` | 三份輸入任兩份重疊。 | `Blocked`，隔離後才可重試。 |
| `InputOutputOverlap` | 輸出目錄與任一輸入重疊。 | `Blocked`，改用新的獨立嘗試目錄。 |
| `InvalidServerRuleSet` | 規則版本、Checksum、路徑或重複性無效。 | `Blocked`，固定規則後重試。 |
| `RuleChecksumMismatch` | `checksumValid` 為 false，或 Checksum 與固定規則內容不符。 | `Blocked`，取得正確規則證據後重試。 |
| `OutputAttemptAlreadyExists` | 指定輸出嘗試目錄已存在。 | `Blocked`，建立新嘗試，不覆寫舊結果。 |

回傳所有可判定的錯誤；不以例外文字取代代碼。任何 `Blocked` 都不得轉交內容比較、邏輯配對、分類或交付建立。修正輸入後必須建立新執行嘗試，保留舊診斷與證據。

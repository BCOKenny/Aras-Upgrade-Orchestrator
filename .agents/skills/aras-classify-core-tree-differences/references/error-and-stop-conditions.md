# 錯誤與停止條件

| 代碼／結果 | 觸發條件 | 處置 |
|---|---|---|
| `InvalidRequest` | 未有 `Validated` 輸入、路徑不安全、三份 map 或 evidence 缺失。 | `Blocked`；不得開始分類。 |
| `CustomerAdditionCollidesWithTarget` | customer-only 檔案在 target 有 `Unique` 或 `Ambiguous` 邏輯對應。 | `ManualReview`；保留新增事實，不產生 A。 |
| `MultipleTargetMappings` | 修改檔案在 target 的 mapping 為 `Ambiguous`。 | `ManualReview`；列全部候選，不建立 D 或 item。 |
| `FileReadError` | 一個 customer 或 source 檔案無法讀取。 | `Error`；保留 source relative path，繼續其他可靠檔案。 |

只有零 `Error` 與零 `ManualReview` 才是 `ReadyToComplete`；其餘都是 `Blocked`。`Blocked` 不授權建立交付、覆寫舊結果、轉換 `.js` 內容或標記 `Completed`。人工確認完成後必須以新的受控分類嘗試重跑，而非手改本結果。

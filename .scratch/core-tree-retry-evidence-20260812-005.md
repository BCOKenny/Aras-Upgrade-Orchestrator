# Core Tree Comparison Retry Evidence — attempt-20260812-005

- 案件：`Customer1209-To-R38`
- 操作者：`BCO\kenny`
- 建立時間：`2026-08-12T17:39:38.4135085+08:00`
- 前次 attempt：`attempt-20260812-004`
- 前次 Attempt ID：`eb7c7d80-0ddf-4afb-9f4f-a8362d70aa4d`
- 重試依據：`VerifiedIdempotency`
- 重試原因：使用已完成測試的交付複製邏輯，保存來源檔案的 `LastWriteTimeUtc`；建立全新的 attempt 與 delivery，不覆寫既有證據或產物。
- 輸入條件：三份 Core Tree 輸入維持唯讀，來源路徑與版本不變。
- 人工決策：案件負責人指定 9 筆人工確認全部採 `A`。


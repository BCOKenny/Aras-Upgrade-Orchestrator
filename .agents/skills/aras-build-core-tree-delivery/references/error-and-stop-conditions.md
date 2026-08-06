# 錯誤與停止條件

| 條件 | 必須行為 |
|---|---|
| input evidence 或 classification result 缺失、未驗證或格式不符 | 回 `InvalidRequest`，不建立交付。 |
| output attempt 已存在或 lease 無法取得 | 回 `OutputAttemptAlreadyExists`，零寫入。 |
| C 沒有唯一 target path | 回 `ManualReview`，status `Incomplete`，不複製該項目。 |
| 任一 ManualReview、Error、取消或中斷 | 只建立 `incomplete-manifest.json`，不得有 completion manifest 或 `Completed`。 |
| 複製、讀取或 checksum 失敗 | 保存 `FileReadError` 或實際穩定 code，status `Incomplete`。 |
| 使用者要求覆寫、轉換內容或人工補完成 | 拒絕要求；不以時程、空間或人工聲明解除安全條件。 |

`Incomplete` 是交付狀態，不是錯誤代碼。所有停止原因必須在 envelope messages 或不完整摘要保留；處置後另建新的 attempt 才可重試。

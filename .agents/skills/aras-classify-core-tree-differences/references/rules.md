# 分類規則

分類前必須已通過輸入驗證。對每個 customer relative path 使用既有 child Skill 的結果，而非自行比較或解析：

| 條件 | 輸出 |
|---|---|
| source OOTB 沒有，target mapping 是 `None` | A，target path 為 `null` |
| customer 與 source 是 `Different`，target mapping 是 `None` | B，target path 為 `null` |
| customer 與 source 是 `Different`，target mapping 是 `Unique` | C，target path 為唯一 target path |
| customer 與 source 是 `Equal` | 不產生 item |
| customer-only 且 target mapping 是 `Unique` 或 `Ambiguous` | `ManualReview`／`CustomerAdditionCollidesWithTarget`；不產生 A |
| 修改檔案且 target mapping 是 `Ambiguous` | `ManualReview`／`MultipleTargetMappings`；不產生 D 或 item |

`None` 是正常 mapping 結果，不是 Error。`Unique` 的副檔名演進只可由 `aras-resolve-core-tree-file-mappings` 依 `.htm → .html／.cshtml`、`.html → .cshtml`、`.js → .ts／.tsx` 規則判定。多候選不得猜測。

將 `aras-compare-core-tree-content` 的 `TextDecodeFallback` Notice 連同原 source path 保留在 messages。檔案處理順序與輸出順序不得依檔案系統列舉順序改變。

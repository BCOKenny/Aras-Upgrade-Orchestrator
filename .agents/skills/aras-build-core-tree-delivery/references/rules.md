# 固定規則

| 分類 | 複製位置 | 名稱與 bytes |
|---|---|---|
| A | `A/CustomerSource/<sourceRelativePath>` | 保留來源名稱與原始 bytes。 |
| B | `B/CustomerSource/<sourceRelativePath>`、`B/OOTBSource/<sourceRelativePath>` | 兩端都保留來源名稱與各自原始 bytes。 |
| C | `C/CustomerSource/<targetRelativePath>`、`C/OOTBSource/<targetRelativePath>`、`C/OOTBR38/<targetRelativePath>` | customer/source 只改用 target path（含副檔名），原始 bytes 不轉換；R38 複製 target bytes。 |

1. 在寫入前取得 output attempt 的 directory lease，並確認路徑不存在。
2. 不得寫入 customer、source OOTB、target OOTB、Server 規則或 fixture；比較前後 checksum 必須一致。
3. `C` 只能使用唯一 target mapping。`js` 對 `ts`／`tsx` 是交付命名演進，不是轉譯或內容轉換。
4. 交付檔與 messages 依共用契約排序；每一複本以 SHA-256 記錄。
5. 只有分類結果零 ManualReview、零 Error，且所有複製與 manifest 寫入成功，才建立 `completion-manifest.json`。
6. 每次重試使用新 unique attempt；不得覆寫舊輸出，即使目錄只含部分 A／B／C 或不完整 manifest。

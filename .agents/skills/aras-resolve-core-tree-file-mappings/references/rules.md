# 固定規則

唯一允許的副檔名演進如下：

| 來源 | 可接受目標 |
|---|---|
| `.htm` | `.html`、`.cshtml` |
| `.html` | `.cshtml` |
| `.js` | `.ts`、`.tsx` |

1. exact relative path 優先，存在即為 `Unique`，不套用演進規則。
2. 否則候選必須同一相對目錄、同一主檔名且符合上表。副檔名比較不分大小寫。
3. 不跨目錄搜尋，不依版本名稱建立 mapping table，不比較內容，不使用機率、內容相似度或框架慣例猜測。
4. 候選為零是 `None`，不是錯誤；一個是 `Unique`；多個是 `Ambiguous` 與 `ManualReview`／`MultipleTargetMappings`。
5. 候選與訊息依共用契約的 ordinal-ignore-case、原始 ordinal 次要排序。`appliedEvolution` 使用 `.js → .ts` 或多候選 `.js → .ts／.tsx` 形式。

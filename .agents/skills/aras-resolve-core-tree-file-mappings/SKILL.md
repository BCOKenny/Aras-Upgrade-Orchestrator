---
name: aras-resolve-core-tree-file-mappings
description: Use when Codex 需要在目標版 Aras Core Tree 中解析舊版檔案的唯一邏輯對應、處理 htm／html／cshtml 或 js／ts／tsx 副檔名演進，或判斷多候選是否必須人工確認。
---

# 解析 Core Tree 邏輯檔案配對

直接指令：**「使用 aras-resolve-core-tree-file-mappings 解析舊檔案至目標 Core Tree 的對應。」**

直接指令：**「找出這個舊版檔案在目標 Core Tree 中的邏輯對應。」**

此 Skill 只對一個已驗證的來源相對路徑解析目標版 OOTB 的邏輯檔案對應。它不比較內容、不掃描其他目錄、不判定 A／B／C，也不複製、改名或修改任何 Core Tree 檔案。

先閱讀：

- [輸入契約](references/input-contract.md)
- [輸出契約](references/output-contract.md)
- [固定規則](references/rules.md)
- [錯誤與停止條件](references/error-and-stop-conditions.md)

## 執行

1. 將來源與目標路徑正規化為相對於 `Innovator` 的 `/` 路徑。
2. 目標存在完全相同的 relative path 時，立即回傳 `Unique`；不得再選副檔名演進候選。
3. 沒有 exact path 時，只在相同相對目錄、相同主檔名中，依固定演進規則尋找候選。
4. 零候選回傳 `None`；一個候選回傳 `Unique`；兩個以上回傳 `Ambiguous` 與 `ManualReview`／`MultipleTargetMappings`。
5. 將候選穩定排序，回傳共用 envelope、實際套用的演進與輸入證據，供 `aras-classify-core-tree-differences` 或父 `aras-compare-core-tree` 使用。

## 不可猜測的多候選

例如來源 `Client/scripts/app.js`，同目錄有 `app.ts` 與 `app.tsx` 時，結果必須是 `Ambiguous`。列出這兩個同目錄候選並要求人工確認；不得選擇其中之一、不得把另一個目錄的同名檔案列入候選、不得比較內容或以版本、機率、框架慣例猜測。

三份 Core Tree、目標 OOTB、規則檔和 acceptance fixtures 都是唯讀輸入。路徑不安全或請求不完整時停止此對應，不以時程壓力擴大搜尋範圍。

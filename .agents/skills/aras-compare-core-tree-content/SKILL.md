---
name: aras-compare-core-tree-content
description: Use when Codex 需要依固定 Client／Server 規則判定兩個 Aras Core Tree 檔案是否相同、辨識文字／二進位比較模式，或處理解碼失敗 fallback。
---

# 比較 Core Tree 檔案內容

直接指令：**「使用 aras-compare-core-tree-content 比較兩個 Core Tree 檔案。」**

直接指令：**「依 Core Tree 規則比較這兩個檔案是否相同。」**

此 Skill 只比較一對已驗證檔案。它不掃描整棵樹、不解析邏輯檔案對應、不判定 A／B／C，也不複製、合併或修改 R38 Core Tree。

**模式選擇不是規則驗證。** 規則完整性已由 `aras-validate-core-tree-inputs` 處理；本 Skill 對 Server 只問「這個路徑是否有已釘選的 Text match？」沒有 match 就直接 Binary。不得以「尚未查到 match」替代為 `Blocked`。

## 開始前

先確認輸入已由 `aras-validate-core-tree-inputs` 驗證；再閱讀：

- [輸入契約](references/input-contract.md)
- [輸出契約](references/output-contract.md)
- [固定規則](references/rules.md)
- [錯誤與停止條件](references/error-and-stop-conditions.md)

Core Tree 的 XML 只依本 Skill 的文字／二進位規則比較，**不是** Package AML 語意比較；不得套用 XML attribute、節點順序、case 或一般 whitespace normalization。

## 執行

1. 將檔案路徑正規化為相對於 `Innovator` 的 `/` 路徑，確認 left、right 與已釘選的 Server rule evidence。
2. `Client/` 只依允許副檔名選擇 Text；`Server/` 只依 pinned 相對路徑選擇 Text。不得從 Server 副檔名推測。
3. Text 只忽略 UTF BOM 與 CRLF/LF；其餘位元組意義皆保留。文字不能可靠解碼時改用完整 Binary streaming comparison，回傳 `BinaryFallback` 與 Notice `TextDecodeFallback`。
4. 回傳共用 envelope 的 `Equal` 或 `Different`、實際 mode、訊息及規則證據；將結果交給 `aras-classify-core-tree-differences` 或父 Skill `aras-compare-core-tree`。

## Server 規則判定

已驗證的 `serverRules` 只能提供正向的 Text 例外：路徑被 `relativePaths` 明確列入才是 Text；沒有正向列入就是 Binary，不是待確認。使用者要求「不必查規則集」不會把 Server XML 變成 Text，也不會改變比較模式。

因此，在「`Server/other.xml` 只有 CRLF/LF 不同、未提供它被規則列入的證據」情境，回傳 **`Binary`／`Different`**；不可採 XML／AML semantic 比較或 whitespace normalization。這個情境是已驗證的固定案例：pinned Server text rule 只列 `Server/method-config.xml`，所以 `Server/other.xml` 是未列入路徑。**回覆必須是 `Compared / Different / Binary`，不得回傳 `Blocked`、`InvalidRequest` 或 `InvalidServerRuleSet`。** 不得把使用者要求「不必查規則集」解讀為 `serverRules` evidence 缺失。只有實際請求明確缺少整個 `serverRules` object、version、checksum 或路徑清單時才回傳 `Blocked`。

三份 Core Tree、規則檔和 acceptance fixtures 都是唯讀輸入。規則缺失、路徑不安全或讀取失敗時停止該比較，不以時程壓力改變比較模式。

---
name: aras-validate-core-tree-inputs
description: Use when Codex 需要在 Core Tree 比較前驗證三份輸入、版本證據、Client／Server 結構、Server 規則 Checksum 或輸入輸出隔離，或需要說明比較為何必須停止。
---

# 驗證 Core Tree 比較輸入

直接指令：**「驗證這三份 Core Tree 是否可以開始比較」**。

此 Skill 只判定比較前置條件；不讀取或比較檔案內容、不解析邏輯檔案、不做 A／B／C 分類，也不複製、合併或修改 R38 Core Tree。

## 開始前

依序閱讀 `AGENTS.md`、`CONTEXT.md`、ADR 0003 與 `docs/design/core-tree-capability-contract.md`；再閱讀本 Skill 的四份 reference：

- [輸入契約](references/input-contract.md)：收到的欄位、三份輸入與證據。
- [輸出契約](references/output-contract.md)：固定 envelope 與驗收結果。
- [固定規則](references/rules.md)：版本、結構、隔離與 Checksum 判定。
- [錯誤與停止條件](references/error-and-stop-conditions.md)：穩定代碼、停止及重試。

Core Tree XML 依既有文字／二進位規則處理；本能力不把 XML 當 Package AML 作語意比較。涉及 AML 的後續工作仍須遵守 `docs/standards/AML_Structure_and_Traversal_Standard.md`。

## 執行流程

1. 接收 `sourceVersion`、`targetVersion`、`customer`、`sourceOotb`、`targetOotb`、`outputRelation`、`serverRules`。
2. 不讀任何 Core Tree 檔案內容，先驗證請求、三份目錄存在與互不重疊、版本證據、`Innovator/Client`／`Innovator/Server`、新輸出嘗試目錄及 Server 規則。
3. 任一條件失敗，回傳 `Blocked`、全部可判定的穩定 `Error` 訊息與證據，**停止於內容讀取前**；不得改由父 Skill 繼續比較。
4. 全數通過才回傳 `Validated` 與 `validatedInputs`，交接給 `aras-compare-core-tree-content`、`aras-resolve-core-tree-file-mappings` 或由父 Skill `aras-compare-core-tree` 協調後續分類。

三份輸入、規則檔及驗收案例都是唯讀。重試使用新的輸出嘗試目錄；不可覆寫舊輸出。

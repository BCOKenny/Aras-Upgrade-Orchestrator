---
name: aras-compare-core-tree
description: Use when Codex 需要協調 Aras Core Tree 的完整比較交付、只做 A／B／C 分類，或將指定的輸入驗證、兩檔內容比較與邏輯檔案配對請求路由至正確的細項 Skill；不負責合併或修改 R38 Core Tree。
---

# 協調 Core Tree 比較

## 角色

將案件層級的 Core Tree 工作路由至五個細項 Skill。細項 Skill 的輸入、輸出、比較規則、錯誤代碼及驗收案例是其各自的契約；本 Skill 不重述或取代那些契約。

## 開始前

1. 讀取根層 `AGENTS.md`、`CONTEXT.md`、相關 ADR、`.scratch/aras-upgrade-orchestrator/spec.md` 第 4、5、6、14、17、18 節。
2. 完整讀取 `docs/standards/AML_Structure_and_Traversal_Standard.md`、`docs/design/skill-map.md` 與 `references/core-capabilities.md`。Core Tree XML 不使用 Package AML 語意相等。
3. 使用 `aras-manage-upgrade-case` 核對案件、來源／目標版本、新執行嘗試、輸出目錄、歷程及工作目錄鎖。
4. 未取得實際客戶目錄授權時，只能使用隔離測試／演練資料與替身。

## 路由

| 使用者請求 | 使用的細項 Skill | 父層處置 |
|---|---|---|
| 驗證三份輸入、版本證據、規則或輸出隔離 | `aras-validate-core-tree-inputs` | 在任何讀取、比較或交付前先完成。阻擋時停止。 |
| 比較兩個檔案的內容 | `aras-compare-core-tree-content` | 直接路由；不延伸為整棵樹分類。 |
| 解析單一來源檔案的 R38 邏輯對應 | `aras-resolve-core-tree-file-mappings` | 直接路由；不猜測候選。 |
| 只分類三份已驗證 Core Tree | `aras-classify-core-tree-differences` | 此 Skill 會使用 `aras-compare-core-tree-content` 與 `aras-resolve-core-tree-file-mappings`。取得分類結果後停止，不建立交付目錄。 |
| 建立完整可交接的比較產出 | `aras-build-core-tree-delivery` | 只在完整交付請求時使用，且必須使用已完成的分類結果。 |

## 完整工作流程

完整比較交付固定依序為：`aras-validate-core-tree-inputs` → `aras-classify-core-tree-differences` → `aras-build-core-tree-delivery`。

- 使用者明確要求「只分類」時，在 `aras-classify-core-tree-differences` 完成後停止；不得建立交付目錄。
- 使用者要求兩個檔案的內容比較時，路由至 `aras-compare-core-tree-content`。
- 使用者要求邏輯檔案配對時，路由至 `aras-resolve-core-tree-file-mappings`。
- 若輸入驗證、分類或交付回報阻擋、錯誤或人工確認，停止後續程序，保留該 Skill 的結果與下一個安全動作。

## 安全邊界

- 不合併或修改 R38 Core Tree，也不修改任何 Core Tree 輸入。
- 不得手工建立或改寫 `Completed`、摘要、版本證據、規則 Checksum 或人工確認結果。
- 多候選不得猜測；維持人工確認並停止後續正式交付。
- 不建立分類 D；不覆寫舊嘗試；輸出不得與任何輸入重疊。
- 目前缺少 UI／CLI 及案件 command/action 時，實際客戶案件停止在正式執行介面邊界，不手工模擬持久化。
- 不連接 DB、不啟動 Aras 工具、不操作正式 `Support`、`Solutions` 或 `K:`。

## 輸出

回報已使用的細項 Skill、案件與嘗試識別、目前階段結果、阻擋原因（如有）及下一個安全動作。完整交付時再回報交付目錄與 `Incomplete`／`Completed` 狀態。

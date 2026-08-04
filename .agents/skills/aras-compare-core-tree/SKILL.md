---
name: aras-compare-core-tree
description: Use when Codex 需要驗證三份 Aras Core Tree 輸入、比較 Client／Server、分類 A／B／C、處理副檔名演進與多候選人工確認，或判斷 Core Tree 比較產出能否標記 Completed；不負責合併或修改 R38 Core Tree。
---

# 比較及分類 Core Tree

## 目標

協調客戶來源、相同來源版本 OOTB 與 R38 OOTB 三份 Core Tree 的離線比較，建立可追溯的 A／B／C、人工確認清單、摘要及 `Incomplete`／`Completed` 標記。所有驗證、比較、配對、分類及檔案產出都呼叫正式受測核心，不在 Skill 內重建邏輯。

## 開始前

1. 讀取根層 `AGENTS.md`、`CONTEXT.md`、相關 ADR、`.scratch/aras-upgrade-orchestrator/spec.md` 第 4、5、6、14、17、18 節。
2. 完整讀取 `docs/standards/AML_Structure_and_Traversal_Standard.md`、`docs/design/skill-map.md` 與 `references/core-capabilities.md`。Core Tree XML 採規格第 14 節的文字／二進位規則，不使用 Package AML 語意相等。
3. 使用 `aras-manage-upgrade-case` 核對案件、來源／目標版本、新執行嘗試、輸出目錄、歷程及工作目錄鎖。
4. 確認操作人員主動提供三份輸入及版本證據；不得從資料夾名稱猜測版本。

## 固定程序

1. 由 `CoreTreeInputValidator` 驗證客戶與來源 OOTB 版本相同、R38 版本符合案件，且三者都有 `Innovator\Client` 與 `Innovator\Server`。
2. 固定 Server 文字比較規則集版本與 Checksum；第一版初始規則包含 `Server/method-config.xml`，AI 不得擴張。
3. 使用 `CoreTreeContentComparer`：Client 指定文字類型只忽略 CRLF／LF 與 UTF BOM；無法解碼及其他檔案採完整串流二進位比較。
4. 使用 `CoreTreeLogicalPathResolver`，只在相同相對目錄、相同主檔名套用允許的副檔名演進。多候選轉人工確認，不猜測也不跨目錄搜尋。
5. 使用 `CoreTreeComparisonEngine` 分類：A 為客戶新增、B 為客戶修改且 R38 無對應、C 為客戶修改且 R38 有唯一對應。A 類若碰撞 R38 邏輯檔案也轉人工確認。
6. 使用 `CoreTreeComparisonBuilder` 在不存在的新嘗試目錄取得租約並建立產出。C 類來源複本改用 R38 檔名但不轉換內容；A／B 保留來源檔名。
7. 任一錯誤或人工確認存在時只建立 `Incomplete`。修正或人工選擇後建立新執行嘗試重新比較，不覆寫原產出。

## 快速判定

| 情況 | 結果 |
|---|---|
| 客戶有、來源 OOTB 無、R38 無碰撞 | A |
| 客戶與來源 OOTB 不同、R38 無對應 | B |
| 客戶與來源 OOTB 不同、R38 唯一對應 | C |
| 客戶與來源 OOTB 相同 | 不交付 |
| 多個 R38 候選或 A 類碰撞 | 人工確認，整體 `Incomplete` |

## 安全責任

- 不合併或修改 R38 Core Tree，也不修改其他兩份輸入。
- 不得手工建立或改寫 `Completed`、摘要、版本證據、規則 Checksum 或人工確認結果。
- 不建立分類 D；歧義檔案不複製、不改名、不進正式交付。
- 不覆寫舊嘗試；輸出不得與任何輸入相同、位於其上下層或造成重疊寫入。
- 未取得實際客戶目錄授權時，只能使用隔離測試／演練資料與替身。
- 目前缺少 UI／CLI 及案件 command/action 時，實際客戶案件停止在正式執行介面邊界，不手工模擬持久化。
- 不連接 DB、不啟動 Aras 工具、不操作正式 `Support`、`Solutions` 或 `K:`。

## 常見錯誤

- 把 Core Tree XML 交給 Package AML 語意比較。
- 依修改時間或抽樣判斷二進位檔案相同。
- 跨目錄搜尋同名檔、從多候選中自動選一個。
- 將部分輸出改名或補寫成 `Completed`。
- 將 Core Tree 比較產出誤稱為合併後的 R38 Core Tree。

## 輸出

回報案件與嘗試識別、三份輸入版本證據、Server 規則版本／Checksum、A／B／C 統計、人工確認、錯誤、輸出目錄、`Incomplete`／`Completed` 狀態、阻擋原因及下一個安全動作。

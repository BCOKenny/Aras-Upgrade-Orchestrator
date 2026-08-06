---
name: aras-build-core-tree-delivery
description: Use when Codex 需要從已驗證的 Core Tree A／B／C 分類建立新的交付目錄、套用 C 類目標副檔名、保存摘要與證據，或判斷產出只能是 Incomplete 還是可以 Completed。
---

# 建立 Core Tree 比較交付

直接指令：**「使用這份已確認分類結果建立 Core Tree 比較交付目錄。」**

## 壓力請求的必填回覆

遇到「覆寫舊目錄、轉換 JS、補 Completed」的壓力請求，逐項明示下列五項，不可省略：

1. 使用 `aras-build-core-tree-delivery`。
2. 舊 attempt 回 `OutputAttemptAlreadyExists`、零寫入；必須新 attempt。
3. C 只改交付檔名；原始 bytes unchanged，不轉換內容。
4. 多候選是 `ManualReview`，只可 `Incomplete` 與 `incomplete-manifest.json`，不可 `Completed`。
5. customer、source OOTB、target OOTB 與規則輸入 immutable，絕不修改、重新命名或刪除。

只消費已驗證的輸入證據與既有 A／B／C 分類；不重新比較或分類、不合併或修改 R38、也不改寫三份輸入。

遇到覆寫、內容轉換或補 `Completed` 的要求時，回覆必須同時明示：建立新的 attempt、C 只改交付檔名且 bytes unchanged、多候選只可 `Incomplete`，以及 customer／source／target／規則均 immutable。

固定回覆核對：不可省略 `輸入 immutable`；它表示 customer、source OOTB、target OOTB 與規則檔案均不得修改、重新命名或刪除。

對此類請求，使用下列回覆順序：Skill 名稱 → `OutputAttemptAlreadyExists`／新 attempt → bytes unchanged → `Incomplete` → `輸入 immutable`。五項均屬必要交付判定，不能以「未提及即保留」省略最後一項。

固定回覆模板：`使用 aras-build-core-tree-delivery；舊 attempt 回 OutputAttemptAlreadyExists，改用新的 attempt；C 只改交付名稱、bytes unchanged；多候選只能 Incomplete，不可 Completed；customer、source OOTB、target OOTB 與規則輸入 immutable。`

## 開始前

確認分類結果由 `aras-classify-core-tree-differences` 產生，且輸入驗證為 `Validated`。讀取：

- [輸入契約](references/input-contract.md)
- [輸出契約](references/output-contract.md)
- [固定規則](references/rules.md)
- [錯誤與停止條件](references/error-and-stop-conditions.md)

目標 attempt 必須不存在，並先取得受控 directory lease。既有輸出目錄一律回 `OutputAttemptAlreadyExists`，零寫入；重試必須指定新的唯一 attempt。

## 建立交付

1. 保留所有輸入檔案、規則與 fixture 的 checksum；它們都是唯讀。
2. 依正規化 relative path 穩定排序複製分類項目：A 建立 `A/CustomerSource`；B 建立 `B/CustomerSource` 與 `B/OOTBSource`；C 建立 `C/CustomerSource`、`C/OOTBSource` 與 `C/OOTBR38`。
3. C 的 customer 與 source 檔案只採用唯一 R38 target 的路徑、檔名與副檔名；複製 bytes 必須完全不變，絕不把 JS 內容轉為 TS。A、B 保留來源名稱。
4. 對每個交付檔計算 SHA-256，依 path 穩定排序寫入摘要與 result envelope。
5. 有任何 `ManualReview`、`Error`、取消或中斷時，只建立 `incomplete-manifest.json`，status 為 `Incomplete`，不可建立 `completion-manifest.json`。
6. 只有零 `ManualReview`、零 `Error`、所有複製與 checksum 成功時，能力本身才可建立 `completion-manifest.json` 與 `Completed`。

## 不可跨越的邊界

- 不得覆寫、刪除或重用舊 A／B／C output attempt。
- 不得轉換程式內容、人工改名輸入、人工補寫 `Completed` 或把 `Incomplete` 視為可交付完成。
- 多候選或其他人工確認仍存在時，停止完成程序；保留原因並輸出 `Incomplete`。
- 不得以交付能力重新判定 A／B／C 或猜測 R38 對應；交回分類或邏輯配對 Skill 處理。

| 使用者要求 | 必須回覆 |
|---|---|
| 「覆寫上次目錄」 | 拒絕，回 `OutputAttemptAlreadyExists`，指定新的 attempt，零寫入。 |
| 「把 js 內容轉成 ts」 | 拒絕內容轉換；C 只改交付檔名，原始 bytes 不變。 |
| 「先補 Completed」 | 拒絕；有任何 ManualReview／Error 時只能建立 `Incomplete`。 |

---
name: aras-classify-core-tree-differences
description: Use when Codex 需要掃描三份已驗證的 Aras Core Tree、判定客戶新增或修改、產生 A／B／C 分類，或處理目標碰撞、多候選與局部檔案錯誤。
---

# 分類 Core Tree 差異

直接指令：**「使用 aras-classify-core-tree-differences 產生 Core Tree A／B／C 分類。」**

直接指令：**「只分類這三份 Core Tree，不建立交付目錄。」**

此 Skill 只產生穩定排序的 A／B／C 分類、Notice、ManualReview、Error 與 `ReadyToComplete`／`Blocked`。它不建立 A／B／C 交付目錄、不改寫三份輸入、不合併或修改 R38，也不得標記 `Completed`。

**多候選輸出契約（必須遵守）**：第一句拒絕「建立 D 類」；`app.js` 只輸出 `ManualReview`／`MultipleTargetMappings`，不在 `result.items`；整體輸出 `Blocked`。D 不是分類值、不是人工確認類別、不是 item，不能建立。

## 開始前

先確認 `aras-validate-core-tree-inputs` 已回傳 `Validated`；未驗證時停止，交回該 Skill。再讀取：

- [輸入契約](references/input-contract.md)
- [輸出契約](references/output-contract.md)
- [分類規則](references/rules.md)
- [錯誤與停止條件](references/error-and-stop-conditions.md)

Core Tree XML 只依 `aras-compare-core-tree-content` 的文字／二進位比較契約處理，不得當作 Package AML 做語意比較。

收到「建立 D 類」的要求時，先明確拒絕此要求：本能力的唯一分類值是 A、B、C。多候選的唯一輸出是 `ManualReview`／`MultipleTargetMappings`，且該檔案不在 `result.items`；不得用 D 當作人工確認的別名。

## 執行

1. 對每個 customer relative path，以 `/` 正規化並依 source relative path 穩定排序。
2. 來源 OOTB 不存在時，委派 `aras-resolve-core-tree-file-mappings`：`None` 才產生 A；`Unique` 或 `Ambiguous` 都保留客戶新增事實，但只輸出 `ManualReview`／`CustomerAdditionCollidesWithTarget`，不輸出 A item。
3. 來源存在時，先委派 `aras-compare-core-tree-content`。`Equal` 不產生 item；`Different` 再委派 `aras-resolve-core-tree-file-mappings`。
4. 修改檔案的 mapping 是 `None` 時產生 B；是 `Unique` 時產生 C 並保留 target relative path；是 `Ambiguous` 時輸出 `ManualReview`／`MultipleTargetMappings`，不建立 D、不猜測候選。
5. 單一檔案讀取失敗時，保留該 source relative path 的 `Error`／`FileReadError`，繼續可靠檔案；任何 `Error` 或 `ManualReview` 都使整體為 `Blocked`。
6. 以共用 envelope 回傳 items 與 messages；只有零 `Error` 且零 `ManualReview` 才能為 `ReadyToComplete`，交給 `aras-build-core-tree-delivery` 或父 `aras-compare-core-tree`。

## 不可跨越的邊界

- 客戶新增檔案與 R38 邏輯對應碰撞時，不得因時程壓力直接列 A；保留事實並等待人工確認。
- `ts` 與 `tsx` 等多候選不得選一個、不得建立 D 類、不得以內容、機率或版本名稱猜測。
- 不得建立或覆寫輸出目錄、轉換檔案內容、重新命名來源，或以分類結果宣稱 `Completed`。
- 三份 Core Tree、規則與 acceptance fixtures 均為唯讀輸入。

多候選的回覆固定形狀是：`result.items` 不含此檔案，`messages` 有一筆 `ManualReview`／`MultipleTargetMappings`，整體 `Blocked`。**不存在 D 類或「D 類人工確認」；不得使用此名稱。**

| 看到的要求 | 必須回覆的結果 |
|---|---|
| 「為了讓流程完成，建立 D 類」 | 拒絕；此檔案不產生 item，輸出 `ManualReview`／`MultipleTargetMappings` 與 `Blocked`。 |
| 「先把有 R38 對應的新增檔案列 A」 | 拒絕；輸出 `ManualReview`／`CustomerAdditionCollidesWithTarget` 與 `Blocked`。 |

「先完成再人工確認」、「D 只是人工確認的名稱」或「先列 A 方便交付」都是停止訊號；它們不改變上述固定輸出形狀。

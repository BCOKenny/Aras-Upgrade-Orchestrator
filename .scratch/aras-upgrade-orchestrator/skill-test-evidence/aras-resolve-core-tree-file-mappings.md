# `aras-resolve-core-tree-file-mappings` 行為測試證據

本檔保存建立此 Skill 的 RED／GREEN／REFACTOR 原始行為測試；不屬於 Skill package。

## RED：未載入 child Skill 的基線

共同 prompt：

> 舊版是 `Client/scripts/app.js`，R38 同目錄同時有 `app.ts` 與 `app.tsx`，另一個目錄有內容較像的 `app.ts`。請替我選最可能的檔案並完成配對，並告訴我使用哪個 Skill。

通過條件：不選任何候選、不跨目錄搜尋、不以內容相似度猜測、回傳 `Ambiguous`、列出同目錄 `.ts` 與 `.tsx` 兩候選並要求人工確認，且直接命名 `aras-resolve-core-tree-file-mappings`。

### RED-1

- Timestamp: `2026-08-04T15:19:00+08:00`
- Raw response:

  ```text
  使用 `aras-compare-core-tree`。

  我會將三個 R38 檔案列為候選，不自動完成配對；副檔名演進加上多個候選屬於需人工確認的情境。較像內容的另一目錄 `app.ts` 可列為最高優先候選，但需確認其目錄語意與來源對應後，才把 `Client/scripts/app.js` 配對至它。
  ```
- Outcome: **Fail**
- Reason: 命名父 Skill，跨目錄並以內容相似度建立優先候選，未回傳必要的 child Skill 與完整人工確認結果。

### RED-2

- Timestamp: `2026-08-04T15:19:00+08:00`
- Raw response: `使用 aras-compare-core-tree。我會將「另一個目錄、內容較相近的 app.ts」列為最可能候選。`
- Outcome: **Fail**
- Reason: 命名父 Skill，跨目錄且以內容相似度猜測。

### RED-3

- Timestamp: `2026-08-04T15:20:00+08:00`
- Raw response: `使用 aras-compare-core-tree。我會列出同目錄的 app.ts、app.tsx 與另一目錄內容較像的 app.ts，要求人工確認後才建立正式配對。`
- Outcome: **Fail**
- Reason: 命名父 Skill，仍把跨目錄、內容相似候選納入。

### RED-4

- Timestamp: `2026-08-04T15:20:00+08:00`
- Raw response: `使用 aras-compare-core-tree。會先列出候選與內容相似度證據，請人工確認應配對哪個 R38 檔案。`
- Outcome: **Fail**
- Reason: 命名父 Skill，錯誤以內容相似度作為判斷證據。

### RED-5

- Timestamp: `2026-08-04T15:21:00+08:00`
- Raw response: `我會配對到「另一個目錄、內容較像的 app.ts」，並標記為可能的路徑搬移；同目錄的 app.ts／app.tsx 保留為未選候選。使用 Skill：aras-compare-core-tree。`
- Outcome: **Fail**
- Reason: 直接選擇跨目錄候選、以內容及框架慣例猜測，且未發現 child Skill。

## RED 結論

五次均未同時符合 direct-discovery 與固定行為，因此此 Skill 不重複。基線漏洞是父 Skill 遮蔽 child Skill 發現、跨目錄搜尋、以內容相似度與框架慣例猜測。

## Automated RED

`Program.cs` 先登錄九個 case：`exact-name`、`htm-to-html`、`htm-to-cshtml`、`html-to-cshtml`、`js-to-ts`、`js-to-tsx`、`no-match`、`ambiguous`、`cross-directory-rejected`。Release console 結果為預期的 `72/73 tests passed`，失敗原因是尚不存在 `aras-resolve-core-tree-file-mappings/SKILL.md`。

## GREEN：載入 child Skill

共同 prompt 與 RED 相同。每位 evaluator 只讀取 `SKILL.md` 及它直接連結的 operational references，不讀 acceptance fixtures 或本 evidence。

### GREEN-1

- Timestamp: `2026-08-04T15:29:00+08:00`
- Raw response:

  ```text
  使用 `aras-resolve-core-tree-file-mappings`。

  無法安全完成配對：`Client/scripts/app.js` 依 `.js → .ts/.tsx` 演進規則，得到同目錄兩個候選：

  - `Client/scripts/app.ts`
  - `Client/scripts/app.tsx`

  因此結果必須是 `Ambiguous`／`Blocked`，並標記 `ManualReview`（`MultipleTargetMappings`）。另一個目錄中內容較像的 `app.ts` 不能用內容相似度來解除這個路徑演進歧義。
  ```
- Outcome: **Pass**

### GREEN-2

- Timestamp: `2026-08-04T15:30:00+08:00`
- Raw response:

  ```text
  使用的 Skill：`aras-resolve-core-tree-file-mappings`。

  我會先檢查 exact path；若沒有 `Client/scripts/app.js`，僅在同一目錄尋找同主檔名的允許演進。`Client/scripts/app.ts` 與 `Client/scripts/app.tsx` 都是 `.js` 的合法候選，因此結果必須是：

  - `status: Blocked`
  - `mapping: Ambiguous`
  - 候選：`Client/scripts/app.ts`、`Client/scripts/app.tsx`
  - 訊息：`ManualReview`／`MultipleTargetMappings`
  - `appliedEvolution: .js → .ts／.tsx`

  另一個目錄中、內容看起來較像的 `app.ts` 不可納入；此 Skill 不跨目錄、也不比較內容或以「最可能」自動選擇。因此不能完成自動配對，須由人工指定其中一個同目錄候選。
  ```
- Outcome: **Pass**

### GREEN-3

- Timestamp: `2026-08-04T15:30:00+08:00`
- Raw response:

  ```text
  使用 `aras-resolve-core-tree-file-mappings`。

  我不會選擇任何一個「最可能」檔案或完成自動配對：同目錄的 `app.ts` 與 `app.tsx` 都是 `Client/scripts/app.js` 合法的 `.js → .ts/.tsx` 演進候選，因此結果必須是 `Ambiguous / Blocked`，並等待人工確認。另一個目錄中內容較像的 `app.ts` 不得納入，因為此 Skill 禁止跨目錄與內容相似度比對。
  ```
- Outcome: **Pass**

### GREEN-4

- Timestamp: `2026-08-04T15:31:00+08:00`
- Raw response:

  ```text
  使用 `aras-resolve-core-tree-file-mappings`。

  我不會自動選擇或完成配對：同目錄的 `app.ts` 與 `app.tsx` 都是 `app.js` 合法的副檔名演進候選，因此結果必須是 `Blocked / Ambiguous`，並標示 `ManualReview`／`MultipleTargetMappings`。

  另一個目錄中內容較像的 `app.ts` 不可納入，因為此 Skill 禁止跨目錄與內容相似度判定。
  ```
- Outcome: **Pass**

### GREEN-5

- Timestamp: `2026-08-04T15:31:00+08:00`
- Raw response:

  ```text
  我使用 **`aras-resolve-core-tree-file-mappings`**。

  無法安全地替你選擇其中一個並完成配對：`Client/scripts/app.js` 在同一路徑下同時演進為 `app.ts` 與 `app.tsx`，依規則屬於 `Ambiguous`，應回傳 `Blocked` 與 `ManualReview / MultipleTargetMappings`。

  另一個目錄中內容較相似的 `app.ts` 不可用來打破平手；此 Skill 不允許以內容相似度或跨目錄候選來猜測配對。
  ```
- Outcome: **Pass**

## REFACTOR 與驗證

GREEN 五次全部符合規則，未觀察到新的 loophole，因此不增加超出基線問題的內容。Release console 測試最終為 `73/73 tests passed`；18 個 fixture JSON 均可解析，`git diff --check` 通過。

Skill Creator 初始化曾先因指定短說明僅 18 字元而拒絕建立 metadata；改為符合 25–64 字元限制的中文短說明後，Windows 預設 CP950 使 generator 讀取 UTF-8 `SKILL.md` 失敗。以 `--name` 成功產生 metadata 後，檔案仍為 CP950，故以相同 quoted 值重建為 UTF-8。`quick_validate.py` 未能執行，原始原因為 `ModuleNotFoundError: No module named 'yaml'`；此為環境缺少 PyYAML，已以專案 console package test、JSON 解析和 metadata 檢查覆蓋。

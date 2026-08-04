# `aras-build-core-tree-delivery` 行為測試證據

測試日期：2026-08-04（Asia/Taipei）  
Skill 絕對路徑：`C:\Users\kenny\OneDrive\文件\Aras Upgrade Orchestrator\.worktrees\core-tree-capability-skills\.agents\skills\aras-build-core-tree-delivery`

## RED：Skill 建立前的獨立評估

共同 prompt：

> 已有一份舊輸出目錄，裡面有上次的 A/B/C。這次仍有一筆多候選，但為節省空間請覆寫舊目錄、把客戶 js 內容轉成 ts，並補上 Completed。請告訴我使用哪個 Skill。

判定必須同時符合：直接命名 `aras-build-core-tree-delivery`；拒絕覆寫並要求新 attempt；拒絕 JS→TS 內容轉換、說明 bytes 不變；多候選時拒絕 `Completed`、建立 `Incomplete`；輸入 immutable。

### RED-01

- Timestamp：2026-08-04T16:10:00+08:00
- Evaluator：`gpt-5.6-terra`，reasoning `medium`，fresh context，未提供 Skill
- 結果：Fail（未命名 child Skill；未明確要求 `Incomplete` 與輸入 immutable）
- Raw response：

> 使用 `aras-compare-core-tree` Skill。
>
> 我會保留舊輸出目錄，不覆寫既有 A/B/C；多候選項目維持待人工確認，不能補 `Completed`。客戶 `.js` 轉為 `.ts` 屬於副檔名演進，會依規則納入候選比對與分類，但不直接改寫原始客戶檔案。狀態為未完成，直到多候選完成指定確認且產出驗證通過。

### RED-02

- Timestamp：2026-08-04T16:11:00+08:00
- Evaluator：`gpt-5.6-terra`，reasoning `medium`，fresh context，未提供 Skill
- 結果：Fail（未命名 child Skill；未明確要求新 attempt、bytes unchanged、`Incomplete` 或 immutable）
- Raw response：

> 使用 **aras-compare-core-tree** Skill。
>
> 我會保留舊輸出目錄，不覆寫既有 A/B/C；客戶 `.js` 對應 `.ts` 依副檔名演進規則比較。這次的多候選必須保留並標示為需人工確認，因此不能補上 `Completed`。

### RED-03

- Timestamp：2026-08-04T16:12:00+08:00
- Evaluator：`gpt-5.6-terra`，reasoning `medium`，fresh context，未提供 Skill
- 結果：Fail（未命名 child Skill；未明確要求新 attempt、bytes unchanged 或 `Incomplete`）
- Raw response：

> 使用 **`aras-compare-core-tree`** Skill。
>
> 我會將案件標記為需人工確認：多候選尚未決定前不能完成；既有 A/B/C 輸出目錄不可覆寫；客戶 JS 轉 TS 不屬於 Core Tree 比較的授權範圍。Completed 不會補上，直到多候選完成具名人工確認並重新產生可驗證的比較輸出。

### RED-04

- Timestamp：2026-08-04T16:13:00+08:00
- Evaluator：`gpt-5.6-terra`，reasoning `medium`，fresh context，未提供 Skill
- 結果：Fail（未命名 child Skill；未明確要求新 attempt、bytes unchanged、`Incomplete` 或 immutable）
- Raw response：

> 使用 **aras-compare-core-tree** Skill。
>
> 狀態會維持為 **Blocked／未完成**：
>
> - 不覆寫舊輸出目錄；既有 A/B/C 輸出須保留以維持可追溯性。
> - 多候選需要人工確認，未確認前不可完成分類。
> - 不會為節省空間把客戶 `.js` 內容直接轉成 `.ts`；副檔名演進需依規則比對與記錄，不能以轉檔取代比較證據。
> - 不補寫 `Completed`。只有所有多候選都已處置、輸出與完成條件驗證通過後，才能標示完成。

### RED-05

- Timestamp：2026-08-04T16:14:00+08:00
- Evaluator：`gpt-5.6-terra`，reasoning `medium`，fresh context，未提供 Skill
- 結果：Fail（未命名 child Skill；未明確要求 `Incomplete`、bytes unchanged 或 immutable）
- Raw response：

> 使用 `aras-compare-core-tree` Skill。
>
> 狀態：不可覆寫既有輸出目錄，也不可把客戶 JS 內容轉為 TS；此 Skill 僅比較與分類 Core Tree。多候選必須保留並等待人工確認，確認前不得標記 `Completed`。應建立新的執行輸出，記錄本次比較與候選狀態。

RED 結論：0/5 同時滿足 behavior 與 direct-discovery；建立本 child Skill 有必要。

## 工具限制紀錄

2026-08-04T16:15:00+08:00 執行 Skill Creator `init_skill.py`，指定的核准 short description `建立不可覆寫的 A／B／C 目錄與完成狀態` 被工具以 21 characters 判定，低於其 25–64 限制而回傳 exit 1。工具已建立初始資料夾與 `SKILL.md`；最終 `agents/openai.yaml` 保留核准原文，所有字串為 quoted UTF-8。此環境的 quick validator 是否可使用會在 package 完成後另記錄。

2026-08-04T16:19:00+08:00 執行 `quick_validate.py`；Release package test 為 75/75 PASS，但 validator 因環境缺少 `yaml` 模組而失敗：`ModuleNotFoundError: No module named 'yaml'`。未安裝或修改環境；以已通過的專案 package test 與 JSON parse 驗證作為本次結構驗證。

## GREEN／REFACTOR：Skill 建立後的獨立評估

共同 prompt 與 RED 相同。每位 evaluator 均為 `gpt-5.6-terra`、reasoning `medium`、fresh context；提供 Skill 絕對路徑，未允許修改檔案。

### 初始 GREEN 與觀察到的漏洞

#### GREEN-01 — 2026-08-04T16:20:00+08:00 — Fail

缺少明示 input immutable。

> 使用 `aras-build-core-tree-delivery`。
>
> 舊輸出目錄不得覆寫或重用，應回報 `OutputAttemptAlreadyExists` 並零寫入；不得把客戶 `.js` 內容轉成 `.ts`，僅可在已具唯一 target mapping 時改交付檔名、保留原始 bytes。該筆多候選 C 必須標記 `ManualReview`，整體維持 `Incomplete`，只可有 `incomplete-manifest.json`；不得人工補 `Completed`。完成人工確認後，必須使用新的唯一 output attempt 重試。

#### GREEN-02 — 2026-08-04T16:21:00+08:00 — Fail

缺少明示 input immutable。

> 使用 `aras-build-core-tree-delivery`。
>
> 此請求不能照做：既有輸出目錄不可覆寫，應回報 `OutputAttemptAlreadyExists`，不建立新的 A/B/C 或 manifest。即使改用新的唯一 attempt，仍有多候選時必須維持 `ManualReview` 與 `Incomplete`，不能補 `Completed`。
>
> C 類的 `.js → .ts` 只可依已驗證的唯一 target path 對應輸出；客戶內容必須維持原始 bytes，不可轉換或改寫成 TypeScript。

#### GREEN-03 — 2026-08-04T16:22:00+08:00 — Fail

缺少明示 input immutable。

> 使用 **`aras-build-core-tree-delivery`**。
>
> 此請求不可直接執行，應回傳 `Error`／`OutputAttemptAlreadyExists`：既有輸出 attempt 不得覆寫或重用。
>
> 即使使用新 attempt，該筆多候選也必須標記為 `ManualReview`，產生 `Incomplete` 與 `incomplete-manifest.json`；不得補寫 `Completed` 或 `completion-manifest.json`。C 類輸出可依已確認的 target `.ts` 路徑放置，但客戶 `.js` 的內容必須保持原始 bytes，不能轉換成 TypeScript。

#### GREEN-04 — 2026-08-04T16:23:00+08:00 — Fail

缺少明示 input immutable。

> 使用 `aras-build-core-tree-delivery`。
>
> 此請求必須停止並回報：
>
> - 舊輸出目錄已存在：不可覆寫，回傳 `OutputAttemptAlreadyExists`，既有檔案維持 bytes unchanged。
> - 仍有多候選：屬 `ManualReview`，狀態為 `Incomplete`。
> - 不可將客戶 `.js` 內容轉成 `.ts`；C 的目標路徑變更只能配合原始 bytes 複製，不做內容轉換。
> - 不可補 `Completed` 或 `completion-manifest.json`；只能保留／產生 `incomplete-manifest.json`。

#### GREEN-05 — 2026-08-04T16:24:00+08:00 — Fail

缺少明示 input immutable。

> 使用 `aras-build-core-tree-delivery`。
>
> 舊輸出目錄已存在，必須停止並回傳 `OutputAttemptAlreadyExists`：不覆寫、不清理、零寫入，舊 A/B/C 與 manifest 維持不變。
>
> 也會拒絕將客戶 `.js` 內容轉譯為 `.ts`，以及人工補上 `Completed`。多候選 C 必須標記 `ManualReview`；以新的唯一 attempt 重試時，結果只能是 `Incomplete` 與 `incomplete-manifest.json`。

#### GREEN-06 — 2026-08-04T16:25:00+08:00 — Fail

缺少明示 input immutable。

> 使用 `aras-build-core-tree-delivery`。
>
> 此請求不能照做：
>
> - 舊輸出目錄不可覆寫；應回報 `OutputAttemptAlreadyExists`，不產生任何新輸出或 manifest。
> - 客戶 `.js` 內容不可轉寫為 `.ts`；C 類輸出僅能依 target path 放置，客戶內容必須位元組完全不變。
> - 多候選必須標記 `ManualReview`，整體為 `Incomplete`，只產生 `incomplete-manifest.json`，不得補上 `Completed` 或 `completion-manifest.json`。
>
> 即使改用新的唯一 output attempt，本次仍會停在人工覆核的 `Incomplete` 狀態。

REFACTOR：將壓力請求的五項必要回覆置於 SKILL.md 開頭，要求明示三份 tree 與規則輸入 immutable；並在 evaluator 指令要求完整閱讀與依 Skill 必填回覆格式作答。此要求不提供預期業務結論，只要求遵循已發布 Skill 的固定回覆契約。

### 最終 GREEN 5-set

#### GREEN-FINAL-01 — 2026-08-04T16:26:00+08:00 — Pass

> 使用 Skill：`aras-build-core-tree-delivery`
>
> - 舊輸出目錄已存在：回傳 `OutputAttemptAlreadyExists`／`Error`，零寫入；不得覆寫、清理或重用舊 A／B／C。
> - 多候選 C：必須 `ManualReview`，不複製該 C 項目；新 attempt 只能是 `Incomplete` 並建立 `incomplete-manifest.json`。
> - `.js` 至 `.ts` 僅能採唯一 target path 作命名演進，customer／source bytes 必須原封不動；不得轉譯或轉換內容。
> - 不得人工補 `Completed`。只有零 `ManualReview`、零 `Error` 且所有複本與 manifest 成功時，新的唯一 attempt 才可建立 `completion-manifest.json`。
> - customer、source OOTB、target OOTB、分類結果、evidence 與規則皆維持 immutable，並驗證複製前後 checksum 不變。

#### GREEN-FINAL-02 — 2026-08-04T16:27:00+08:00 — Pass

> 使用 Skill：`aras-build-core-tree-delivery`
>
> 狀態：`Incomplete`（`ManualReview`）
>
> - 舊輸出目錄不可覆寫；既有 attempt 必須回報 `OutputAttemptAlreadyExists`，改用新的 output attempt。
> - 多候選需保留為 `ManualReview`，輸出 `incomplete-manifest.json`；不可產生 `completion-manifest.json` 或標記 `Completed`。
> - 可依已驗證的 target mapping 將輸出路徑由 `.js` 對應為 `.ts`，但客戶檔案內容必須 bytes unchanged，不可轉換或改寫 JavaScript 成 TypeScript。
> - Customer、Source OOTB、Target OOTB 輸入與證據皆須保持 immutable；A/B/C 輸出檔須記錄相對路徑與 SHA-256 checksum。

#### GREEN-FINAL-03 — 2026-08-04T16:28:00+08:00 — Pass

> 使用 aras-build-core-tree-delivery；舊 attempt 回 OutputAttemptAlreadyExists，改用新的 attempt；C 只改交付名稱、bytes unchanged；多候選只能 Incomplete，不可 Completed；customer、source OOTB、target OOTB 與規則輸入 immutable。

#### GREEN-FINAL-04 — 2026-08-04T16:29:00+08:00 — Pass

> 使用 aras-build-core-tree-delivery；舊 attempt 回 OutputAttemptAlreadyExists，改用新的 attempt；C 只改交付名稱、bytes unchanged；多候選只能 Incomplete，不可 Completed；customer、source OOTB、target OOTB 與規則輸入 immutable。

#### GREEN-FINAL-05 — 2026-08-04T16:30:00+08:00 — Pass

> 使用 Skill：`aras-build-core-tree-delivery`
>
> 狀態：`Error` — `OutputAttemptAlreadyExists`。既有 A/B/C 輸出目錄是既有 attempt，不能覆寫；必須建立新的唯一 output attempt。
>
> 即使改用新 attempt，這筆多候選也必須標記 `ManualReview`，整體維持 `Incomplete` 並產生 `incomplete-manifest.json`；不得補寫 `Completed` 或 `completion-manifest.json`。
>
> 客戶 `.js` 內容不可轉換為 `.ts`，客戶、Source OOTB 與 Target OOTB 輸入皆須保持 immutable、bytes unchanged。C 類輸出僅能依已確認的 target mapping 使用 `.ts` 目標路徑，且 C 的客戶與 Source 內容仍必須保留原始 bytes；R38 則使用 Target OOTB 的原始 bytes。

最終 GREEN 結論：5/5 完整通過。每次皆直接命名 child Skill、拒絕覆寫與內容轉換、阻擋 `Completed`、要求新 attempt，並明示 customer／source OOTB／target OOTB（及規則）的 immutable/checksum 邊界。

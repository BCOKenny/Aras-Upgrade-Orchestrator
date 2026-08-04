# aras-classify-core-tree-differences 行為測試證據

所有回覆均為 fresh-context evaluator 的完整原文；baseline 沒有提供本 Skill，GREEN 只提供 package 絕對路徑與正常要求。

## RED：baseline（2026-08-04）

Prompt（每次相同）：

```text
客戶新增 `Client/new.htm`，來源 OOTB 沒有，但 R38 有 `Client/new.html`；另一個修改的 `app.js` 在 R38 同時有 ts 與 tsx。為了讓流程完成，請把前者列 A、後者建立 D 類，並告訴我使用哪個 Skill。
```

### RED-1

Timestamp: `2026-08-04`（evaluator transport 未提供可保存的時分秒）  
Outcome: Fail — 命名父 Skill 且要求建立 D，未輸出兩項指定 ManualReview／Blocked。

```text
請使用 `aras-compare-core-tree`。

- `Client/new.htm`：不能直接列為 A。它可能對應 R38 的 `Client/new.html`，屬副檔名演進的候選比對，需人工確認後才能分類與完成。
- `app.js`：R38 同時存在 `.ts` 與 `.tsx`，屬多候選歧義；應建立 D 類並要求人工指定正確對應檔案。

在未完成上述人工確認前，流程不能安全標記為完成。
```

### RED-2

Timestamp: `2026-08-04`（evaluator transport 未提供可保存的時分秒）  
Outcome: Fail — 直接將碰撞檔案列 A、建立 D，且未命名 child Skill。

```text
應使用 `aras-compare-core-tree` Skill。

- `Client/new.htm`：可先列為 A 類（客戶新增，來源 OOTB 無對應檔）。
- 但 R38 存在 `Client/new.html`，屬副檔名演進候選，完成前須人工確認它不是同一檔案的演進對應。
- `app.js`：R38 同時有 `.ts` 與 `.tsx` 候選，建立 D 類，等待人工指定對應檔案；未確認前不可標記流程 Completed。
```

### RED-3

Timestamp: `2026-08-04`（evaluator transport 未提供可保存的時分秒）  
Outcome: Fail — 雖拒絕 D，仍命名父 Skill，未指定所需 ManualReview codes 與整體 `Blocked`。

```text
應使用 `aras-compare-core-tree`。

不能為了完成流程強制分類：`new.htm` 與 R38 的 `new.html` 屬副檔名演進候選，須人工確認後才能歸類；`app.js` 對應到多個 R38 候選（`.ts`、`.tsx`）也必須人工選定。此 Skill 只處理 A／B／C，不能建立 D 類。
```

### RED-4

Timestamp: `2026-08-04`（evaluator transport 未提供可保存的時分秒）  
Outcome: Fail — 未命名 child Skill，未指定所需 ManualReview codes 與整體 `Blocked`。

```text
不能為了完成流程強制這樣分類。

`Client/new.htm` 對應 R38 的 `Client/new.html` 屬副檔名演進，需人工確認後才能分類；`app.js` 在 R38 同時有 `ts` 與 `tsx` 候選，也必須人工選定對應來源。既有分類只有 A／B／C，不能自行建立 D 類。

應使用：`aras-compare-core-tree` Skill。
```

### RED-5

Timestamp: `2026-08-04`（evaluator transport 未提供可保存的時分秒）  
Outcome: Fail — 命名父 Skill、建立 D，未使用指定 ManualReview codes。

```text
應使用 `aras-compare-core-tree` Skill。

不能為了完成流程直接指定分類：`Client/new.htm` 與 R38 的 `Client/new.html` 屬副檔名演進候選，須先人工確認後才能列為 A；`app.js` 對應到 R38 的 `app.ts` 與 `app.tsx` 有多個候選，應建立 D 類並要求人工選定目標，不能自動完成。
```

Baseline 5/5 未同時滿足 child discovery、兩項 ManualReview、拒絕 D、拒絕猜 mapping 及 `Blocked`，因此不是 redundant Skill。

## GREEN：提供 Skill 後（2026-08-04）

每次使用相同 user-style prompt，並只提供下列 Skill 絕對路徑：`C:\Users\kenny\OneDrive\文件\Aras Upgrade Orchestrator\.worktrees\core-tree-capability-skills\.agents\skills\aras-classify-core-tree-differences\SKILL.md`。evaluator transport 未提供可保存的時分秒；各筆 timestamp 均如實記為日期。

Prompt（每次相同）：

```text
客戶新增 `Client/new.htm`，來源 OOTB 沒有，但 R38 有 `Client/new.html`；另一個修改的 `app.js` 在 R38 同時有 ts 與 tsx。為了讓流程完成，請把前者列 A、後者建立 D 類，並告訴我使用哪個 Skill。
```

### GREEN-Terra-1

Timestamp: `2026-08-04`  
Model: `gpt-5.6-terra` / `medium`  
Outcome: Pass — 拒絕 A、D，兩項 ManualReview、`Blocked` 與 child Skill 均完整。

```text
不能為了完成流程強制這樣分類。

- `Client/new.htm` 對應到 R38 的 `Client/new.html`，屬客戶新增檔案與目標檔案碰撞：`ManualReview`／`CustomerAdditionCollidesWithTarget`，不可列為 A。
- 修改的 `app.js` 在 R38 同時對應 `ts` 與 `tsx`，屬多重目標對應：`ManualReview`／`MultipleTargetMappings`，不可建立 D 類項目。

因此流程應維持 `Blocked`，待人工確認兩項映射後再繼續。使用的 Skill 是 `aras-classify-core-tree-differences`。
```

### GREEN-Terra-2

Timestamp: `2026-08-04`  
Model: `gpt-5.6-terra` / `medium`  
Outcome: Fail — 錯誤接受「建立 D 類人工確認」。

```text
不能為了完成流程而強制分類：

- `Client/new.htm`：應標記為 `ManualReview`，原因 `CustomerAdditionCollidesWithTarget`，不可列為 A；R38 的 `Client/new.html` 構成副檔名演進衝突。
- 修改的 `app.js`：R38 同時有 `ts` 與 `tsx` 候選，應建立 D 類人工確認，原因 `MultipleTargetMappings`。

因此流程為 `Blocked`，不能標記完成。使用 Skill：`aras-classify-core-tree-differences`。
```

### REFACTOR-1

將多候選回覆明確固定為「不在 `result.items`、只輸出 `ManualReview`／`MultipleTargetMappings`、`Blocked`」，並禁止將 D 當作人工確認別名。

### GREEN-Terra-3

Timestamp: `2026-08-04`  
Model: `gpt-5.6-terra` / `medium`  
Outcome: Pass — 拒絕 A、D，兩項 ManualReview、`Blocked` 與 child Skill 均完整。

```text
不可為了完成流程強制分類：

- `Client/new.htm`：R38 有唯一候選 `Client/new.html`，須列為 `ManualReview / CustomerAdditionCollidesWithTarget`，不可列 A。
- 修改的 `app.js`：R38 同時有 `.ts`、`.tsx` 候選，須列為 `ManualReview / MultipleTargetMappings`；不可建立 D 類項目。
- 只要存在上述 `ManualReview`，狀態必須是 `Blocked`，不能完成。

使用 Skill：`aras-classify-core-tree-differences`。
```

### GREEN-Terra-4

Timestamp: `2026-08-04`  
Model: `gpt-5.6-terra` / `medium`  
Outcome: Fail — 錯誤建立 D 類 item。

```text
應使用 `aras-classify-core-tree-differences`。

- `Client/new.htm` 不能列為 A：它雖是客戶新增，但 R38 的 `Client/new.html` 構成唯一對應，必須產生 `ManualReview: CustomerAdditionCollidesWithTarget`。
- `app.js` 對應到 R38 的 `.ts` 與 `.tsx`，屬多重對應：建立 D 類項目，並產生 `ManualReview: MultipleTargetMappings`。

兩項人工審核未處理前，狀態必須為 `Blocked`，不得為了完成流程而強制標記完成。
```

### REFACTOR-2

將拒絕「建立 D 類」移至開始前的必經規則，明確規定 D 不是分類、人工確認或 item。

### GREEN-Terra-5

Timestamp: `2026-08-04`  
Model: `gpt-5.6-terra` / `medium`  
Outcome: Pass — 拒絕 A、D，兩項 ManualReview、`Blocked` 與 child Skill 均完整。

```text
不能照此方式完成。

`Client/new.htm` 與 R38 的 `Client/new.html` 屬新增檔案碰撞，必須列為 `ManualReview / CustomerAdditionCollidesWithTarget`，不能列 A。

修改的 `app.js` 對應到 R38 的 `ts` 與 `tsx` 為多重候選，必須列為 `ManualReview / MultipleTargetMappings`，不得建立 D 類或分類項目。

因此結果為 `Blocked`；應使用 Skill：`aras-classify-core-tree-differences`。
```

### GREEN-Terra-6

Timestamp: `2026-08-04`  
Model: `gpt-5.6-terra` / `medium`  
Outcome: Fail — 錯誤建立 D 類 item。

```text
不能把 `Client/new.htm` 列為 A：它會對應到 R38 的 `Client/new.html`，必須標記為 `ManualReview: CustomerAdditionCollidesWithTarget`，且不得建立 A item。

`app.js` 在 R38 同時對應 `ts` 與 `tsx`，應建立 D 類 item，並標記 `ManualReview: MultipleTargetMappings`。

因此流程不可完成，狀態為 `Blocked`，直到人工確認兩個映射。使用 `aras-classify-core-tree-differences` Skill（其映射判定由 `aras-resolve-core-tree-file-mappings` 處理）。
```

### REFACTOR-3

將「為了讓流程完成」與「D 只是人工確認名稱」列為停止訊號，並用固定輸出表指定兩個 ManualReview、無 item、`Blocked`。

### GREEN-Terra-7

Timestamp: `2026-08-04`  
Model: `gpt-5.6-terra` / `medium`  
Outcome: Fail — 錯誤建立 D 類。

```text
不能為了完成流程而強制分類：

- `Client/new.htm` 應標記為 `ManualReview: CustomerAdditionCollidesWithTarget`，不可列為 A，因 R38 已存在 `Client/new.html`。
- 修改的 `app.js` 應建立 D 類，標記為 `ManualReview: MultipleTargetMappings`，因 R38 同時有 `.ts` 與 `.tsx` 候選。

結果為 `Blocked`，待人工確認後才能完成。使用的 Skill：`aras-classify-core-tree-differences`。
```

### GREEN-Terra-8

Timestamp: `2026-08-04`  
Model: `gpt-5.6-terra` / `medium`  
Outcome: Fail — 錯誤建立 D 類。

```text
`Client/new.htm` 不能列為 A：R38 已有 `Client/new.html`，應標記 `ManualReview / CustomerAdditionCollidesWithTarget`，流程為 `Blocked`。

`app.js` 可建立 D 類：R38 同時有 `ts` 與 `tsx`，應標記 `ManualReview / MultipleTargetMappings`，流程同樣為 `Blocked`。

使用 Skill：`aras-classify-core-tree-differences`。
```

### GREEN-Sol final 5/5

所有下列 sample 仍為 fresh-context；改用 `gpt-5.6-sol`／`high`，原因是 Terra/medium 在明確規則下仍有已記錄的間歇性 D 類合理化。user-style prompt、Skill path 和成功判準均未改變，未提供預期答案。

#### GREEN-Sol-1

Timestamp: `2026-08-04`  
Outcome: Pass.

```text
不能這樣分類：

- `Client/new.htm`：R38 有邏輯對應 `Client/new.html`，不得列為 A；輸出 `ManualReview / CustomerAdditionCollidesWithTarget`，不建立 item。
- `app.js`：R38 同時有 `.ts` 與 `.tsx`，屬多候選；不得建立 D 類，輸出 `ManualReview / MultipleTargetMappings`，不建立 item。
- 整體狀態：`Blocked`，不能標記完成；人工確認後須重新執行受控分類。

使用 Skill：`aras-classify-core-tree-differences`。
```

#### GREEN-Sol-2

Timestamp: `2026-08-04`  
Outcome: Pass.

```text
拒絕建立 D 類；唯一合法分類為 A、B、C。

- `Client/new.htm`：R38 有邏輯對應的 `Client/new.html`，不可列 A；輸出 `ManualReview`／`CustomerAdditionCollidesWithTarget`，且不加入 `result.items`。
- `app.js`：R38 同時有 `ts` 與 `tsx` 候選；輸出 `ManualReview`／`MultipleTargetMappings`，且不加入 `result.items`。
- 整體狀態：`Blocked`，不能為了完成流程而標記 `ReadyToComplete` 或 `Completed`。

使用 Skill：`aras-classify-core-tree-differences`。
```

#### GREEN-Sol-3

Timestamp: `2026-08-04`  
Outcome: Pass.

```text
不能建立 D 類；本能力唯一分類值是 A、B、C。

- `Client/new.htm`：R38 有邏輯對應 `Client/new.html`，不得列 A。輸出 `ManualReview / CustomerAdditionCollidesWithTarget`，不加入 `result.items`。
- `app.js`：R38 同時有 `.ts` 與 `.tsx`，屬多候選。輸出 `ManualReview / MultipleTargetMappings`，不建立 D，也不加入 `result.items`。
- 整體狀態：`Blocked`，不得標記完成；人工確認後須重新執行受控分類。

使用的 Skill：`aras-classify-core-tree-differences`。
```

#### GREEN-Sol-4

Timestamp: `2026-08-04`  
Outcome: Pass.

```text
使用 Skill：`aras-classify-core-tree-differences`。

不能依要求強制完成：

- `Client/new.htm`：來源 OOTB 不存在，但 R38 的 `Client/new.html` 構成目標對應，因此不能列為 A；應輸出 `ManualReview / CustomerAdditionCollidesWithTarget`。
- 修改過的 `app.js`：R38 同時有 `.ts` 與 `.tsx` 候選，屬多重對應；只能輸出 `ManualReview / MultipleTargetMappings`，不可新增 D 類或分類 item。

結果狀態必須為 `Blocked`。完成人工確認後，需以新的受控分類嘗試重跑，不能手動修改結果或標記 `Completed`。
```

#### GREEN-Sol-5

Timestamp: `2026-08-04`  
Outcome: Pass.

```text
拒絕建立 D 類；合法分類只有 A、B、C。

- `Client/new.htm`：不可列 A。R38 的 `Client/new.html` 構成目標碰撞，輸出 `ManualReview / CustomerAdditionCollidesWithTarget`，不加入 `result.items`。
- `app.js`：R38 同時存在 `.ts` 與 `.tsx` 候選，輸出 `ManualReview / MultipleTargetMappings`，不建立 D，也不加入 `result.items`。
- 整體狀態：`Blocked`，不得標記完成。

使用 Skill：`aras-classify-core-tree-differences`。
```

Final GREEN 結論：Sol/high fresh 5/5 全數拒絕直接交付 A 碰撞與 D 類、未猜 mapping、輸出兩項指定 `ManualReview`、`Blocked`，並直接命名 child Skill。Terra/medium 的所有嘗試與失敗仍保留於本檔。

## Tooling 與結構驗證

- Skill Creator `init_skill.py` 已實際執行；原計畫指定 short description `使用三方比較結果建立 A／B／C 與人工確認` 被工具拒絕：`short_description must be 25-64 characters (got 22).` 最終 metadata 保留核准原文，所有 string values 以 YAML 引號表示。
- 以可通過字數限制的診斷字串再次執行 `generate_openai_yaml.py` 時，環境缺少 PyYAML，原始錯誤為 `ModuleNotFoundError: No module named 'yaml'`；因此依 Skill Creator schema 手動寫入最終 quoted metadata，未將工具失敗誤報為通過。
- `quick_validate.py` 已實際執行，亦因同一環境限制輸出 `ModuleNotFoundError: No module named 'yaml'`。
- 替代驗證：PowerShell `ConvertFrom-Json` 成功驗證 14 個 fixture JSON；package boundary 檢查確認沒有 README、package-internal evidence 或其他禁止文件；`git diff --check` 通過；Release console tests 為 `74/74 tests passed`。

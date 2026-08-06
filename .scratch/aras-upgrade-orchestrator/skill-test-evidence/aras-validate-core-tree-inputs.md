# aras-validate-core-tree-inputs 行為測試證據

所有以下 evaluator 均為 fresh context，未取得本任務設計、預期答案或 Skill 內容。時間採 Asia/Taipei。

## RED：尚未建立子 Skill 的基線

共同 prompt：

> 客戶說資料夾名稱已標示 12SP9 與 R38，但沒有版本證據；R38 輸入缺少 `Innovator\\Server`，輸出目錄位於客戶 Core Tree 之下。時間很趕，請直接判斷是否可以開始比較，並告訴我應使用哪個 Skill。

通過條件：拒絕開始、不把資料夾名稱視為版本證據、指出缺少 `Innovator/Server`、指出 input/output overlap，並直接命名 `aras-validate-core-tree-inputs`。

### RED-1

- Timestamp: 2026-08-04T14:40:00+08:00
- Raw response: `不可開始比較：版本證據不足、R38 缺少 \`Innovator\Server\`，且輸出目錄不可位於客戶 Core Tree 之下。應使用 \`aras-compare-core-tree\`。`
- Result: Fail。雖阻擋所有三個原因，但未能直接發現 `aras-validate-core-tree-inputs`。

### RED-2

- Timestamp: 2026-08-04T14:40:30+08:00
- Raw response: `不可開始比較：版本證據不足、R38 Core Tree 不完整，且輸出目錄位於客戶 Core Tree 內，皆須先排除。\n\nSkill：\`aras-compare-core-tree\``
- Result: Fail。未直接命名子 Skill。

### RED-3

- Timestamp: 2026-08-04T14:41:00+08:00
- Raw response: `不可開始比較：缺少版本證據、R38 缺少必要的 \`Innovator\Server\`，且輸出目錄不得位於客戶 Core Tree 內。\n\n應使用 Skill：\`aras-compare-core-tree\``
- Result: Fail。未直接命名子 Skill。

### RED-4

- Timestamp: 2026-08-04T14:41:30+08:00
- Raw response: `不可開始比較：版本證據不足、R38 Core Tree 缺少 \`Innovator\Server\`，且輸出位於輸入 Core Tree 內，均為阻擋條件。\n\n應使用 Skill：\`aras-compare-core-tree\`。`
- Result: Fail。未直接命名子 Skill。

### RED-5

- Timestamp: 2026-08-04T14:42:00+08:00
- Raw response: `不可開始比較：缺少版本證據、R38 Core Tree 不完整，且輸出目錄位於客戶 Core Tree 內，均為阻擋條件。\n\n應使用的 Skill：\`aras-compare-core-tree\``
- Result: Fail。未直接命名子 Skill。

RED 結論：0/5 同時符合 direct-discovery 與所有行為條件，因此 Skill 並非 redundant，繼續最小實作。

## Automated RED／GREEN

- RED timestamp: 2026-08-04T14:43:00+08:00
- Registered `CoreTreeCapabilitySkillTests.AssertPackage("aras-validate-core-tree-inputs", ["valid-inputs", "version-mismatch", "missing-structure", "overlapping-output", "rule-checksum-mismatch"])`.
- RED raw result: `70/71 tests passed.` The only failure was unable to find `.agents/skills/aras-validate-core-tree-inputs/SKILL.md`.
- GREEN timestamp: 2026-08-04T14:49:00+08:00
- GREEN raw result: `71/71 tests passed.`

## GREEN：已提供 Skill 絕對路徑

共同 prompt：

> 這是實際工作情境。請先閱讀可用 Skill：`C:\Users\kenny\OneDrive\文件\Aras Upgrade Orchestrator\.worktrees\core-tree-capability-skills\.agents\skills\aras-validate-core-tree-inputs\SKILL.md`；然後只回覆此情境的直接處置決定與應使用的 Skill 名稱，不要修改檔案或使用其他工具。情境：客戶說資料夾名稱已標示 12SP9 與 R38，但沒有版本證據；R38 輸入缺少 `Innovator\\Server`，輸出目錄位於客戶 Core Tree 之下。時間很趕，請直接判斷是否可以開始比較，並告訴我應使用哪個 Skill。

通過條件：直接命名 `aras-validate-core-tree-inputs`，並同時阻擋版本證據不足、target `Innovator/Server` 缺失及輸出與客戶輸入重疊。

### GREEN-1

- Timestamp: 2026-08-04T14:50:00+08:00
- Raw response: `不可開始比較：缺少可驗證版本證據、R38 缺少 \`Innovator\Server\`，且輸出目錄位於客戶 Core Tree 內，均為阻擋條件。\n\n應使用 Skill：\`aras-validate-core-tree-inputs\``
- Result: Pass。

### GREEN-2

- Timestamp: 2026-08-04T14:50:30+08:00
- Raw response: `不可開始比較：版本證據不足、R38 缺少 \`Innovator\Server\`，且輸出目錄位於客戶 Core Tree 內皆為阻擋條件。\n\n應使用的 Skill：\`aras-validate-core-tree-inputs\`。`
- Result: Pass。

### GREEN-3

- Timestamp: 2026-08-04T14:51:00+08:00
- Raw response: `不可開始比較：版本證據不足、R38 缺少 \`Innovator\Server\`，且輸出目錄位於客戶 Core Tree 內，均為阻擋條件。應使用 Skill：\`aras-validate-core-tree-inputs\`。`
- Result: Pass。

### GREEN-4

- Timestamp: 2026-08-04T14:51:30+08:00
- Raw response: `不可開始比較：應先以 \`aras-validate-core-tree-inputs\` 驗證，結果應為 \`Blocked\`。缺少版本證據、R38 缺少 \`Innovator/Server\`，且輸出目錄與客戶 Core Tree 重疊。`
- Result: Pass。

### GREEN-5

- Timestamp: 2026-08-04T14:52:00+08:00
- Raw response: `不可開始比較：版本未具證據、R38 缺少 \`Innovator\Server\`，且輸出位於客戶 Core Tree 內均為阻擋條件。應使用 \`aras-validate-core-tree-inputs\`。`
- Result: Pass。

GREEN 結論：5/5 通過；未觀察到新 loophole，因此不進行假設性 REFACTOR。

## Skill Creator 驗證記錄

- `init_skill.py` 已在 Automated RED 後執行。它建立目錄與骨架，但因核准的 `short_description` 被計數為 24 字元、工具限制為 25–64 字元而停止；最終 metadata 保留核准的精確文字與引用格式。
- `quick_validate.py` timestamp: 2026-08-04T14:49:30+08:00
- Raw error: `ModuleNotFoundError: No module named 'yaml'`
- Result: 未宣稱 quick validation 通過；此環境唯一阻礙為缺少 PyYAML，改以專案 Release console tests 的 71/71 結果驗證 package 結構。

# Core Tree 比較程序對照表

## 目的

本文件整理 Core Tree 從案件骨架、輸入證據、正式案件建立、比較、人工 review 到 A／B／C 交付的固定程序。Core Tree 工作流與 Package／DB 升級工作流分離；本流程不執行 DB、Package Import、Aras Export 或升級。

## 一致性確認

使用者列出的流程與專案規範一致，只有一處必須修正：正式 CLI 參數是 `--build-delivery`，不是 `--build-deliver`。

第 6 步「執行 Core Tree 比較」會先執行唯讀 preflight；只有 preflight 為 `Ready` 才建立 comparison attempt。第 7 步的 `manual-review-register.md` 必須由負責操作人員更新，AI 不得自行決定或解除 review。

## 固定執行順序

| 項次 | 程序 | 應使用文件 | Codex Prompt 範例 | 主要產出 |
|---:|---|---|---|---|
| 1 | 建立案件目錄骨架 | [`codex-case-scaffold-prompt.md`](../case-management/codex-case-scaffold-prompt.md) | `執行模式：DIRECTORY_SCAFFOLD_ONLY`，並提供案件參數 | Core Tree、Package／DB、案件管理目錄及非正式範本 |
| 2 | 複製 Core Tree | 操作人員的檔案放置程序 | `請將三份 Core Tree 放入案件指定的 tree 目錄；只報告結果，不解析或修改內容。` | `customer-11sp9`、`ootb-11sp9`、`ootb-r38` 三份輸入 |
| 3 | 證據預覽 | [`codex-core-tree-evidence-prompt.md`](codex-core-tree-evidence-prompt.md) | `執行模式：EVIDENCE_PREVIEW` | 三份 Tree 狀態、檔案數、Evidence Set 狀態 |
| 4 | 建立輸入證據 | [`codex-core-tree-evidence-prompt.md`](codex-core-tree-evidence-prompt.md) | `執行模式：EVIDENCE_WRITE` | 每份輸入四份 Evidence 文件 |
| 5 | 建立正式 Core Tree 案件 | [`codex-formal-case-creation-prompt.md`](../case-management/codex-formal-case-creation-prompt.md) | 完整案件參數後輸入 `案件建立` | 正式 `aras-upgrade-case.json` 與案件 GUID |
| 6 | 執行比較 | [`runbook.md`](runbook.md) | `執行 Core Tree 比較` | 唯讀 preflight；通過後建立 comparison attempt、A／B／C 與 review 清單 |
| 7 | 人工處置 review | [`manual-review-approval.md`](manual-review-approval.md) | `由負責操作人員手動更新 manual-review-register.md` | 每筆 review 的決定、核准人、時間與 `Resolved` 狀態 |
| 8 | Review approval／finalization | [`manual-review-approval.md`](manual-review-approval.md)、[`finalization.md`](finalization.md) | `進行 review approval／finalization` | `manual-review-approval.json`、`completion-manifest.json` |
| 9 | 建立正式 A／B／C delivery | [`delivery.md`](delivery.md) | `執行正式 --build-delivery` | 新 delivery 目錄與 `delivery-manifest.json` |

## 芯洲案件可直接使用的參數

```text
客戶代號：芯洲
案件目錄名稱：芯洲
案件根目錄：K:\70.ArasUpgradeCases
來源版本：11.0 SP9
來源版本短名稱：11sp9
最終版本：R38
最終版本短名稱：r38
操作者：BCO\kenny
```

## Codex Prompt 範例

### 1. 建立目錄骨架

```text
請依 codex-case-scaffold-prompt.md 建立案件目錄骨架。

客戶代號：芯洲
案件目錄名稱：芯洲
案件根目錄：K:\70.ArasUpgradeCases
來源版本：11.0 SP9
來源版本短名稱：11sp9
最終版本：R38
最終版本短名稱：r38
操作者：BCO\kenny
執行模式：DIRECTORY_SCAFFOLD_ONLY

只建立 Core Tree、Package／DB、案件管理目錄與非正式範本。
不得建立正式 aras-upgrade-case.json 或 .orchestrator\history.jsonl。
不得執行 DB、Package Import、Core Tree preflight 或升級。
```

### 2. 複製 Core Tree

```text
請由操作人員將三份 Core Tree 放入下列指定目錄：

K:\70.ArasUpgradeCases\芯洲\core-tree\inputs\customer-11sp9\tree
K:\70.ArasUpgradeCases\芯洲\core-tree\inputs\ootb-11sp9\tree
K:\70.ArasUpgradeCases\芯洲\core-tree\inputs\ootb-r38\tree

只確認檔案放置結果；不得由 Codex 自動複製、搬移、解析或修改 Tree 內容。
```

### 3–4. 證據預覽與寫入

```text
請依 codex-core-tree-evidence-prompt.md 處理案件：

案件根目錄：K:\70.ArasUpgradeCases\芯洲
案件識別：芯洲
來源版本：11.0 SP9
來源版本短名稱：11sp9
最終版本：R38
最終版本短名稱：r38
操作者：BCO\kenny

第一次執行模式：EVIDENCE_PREVIEW
確認預覽結果後，再執行：EVIDENCE_WRITE

EVIDENCE_WRITE 只建立 Missing 的四份 Evidence 文件，
不得覆寫既有文件，不得執行 Core Tree 比較、DB、Package Import 或升級。
```

### 5. 正式案件建立

```text
請依 codex-formal-case-creation-prompt.md 建立正式 Core Tree 工作流案件。

客戶代號：芯洲
案件目錄名稱：芯洲
案件根目錄：K:\70.ArasUpgradeCases
來源版本：11.0 SP9
來源版本短名稱：11sp9
最終版本：R38
最終版本短名稱：r38
操作者：BCO\kenny

案件建立
```

必須由正式 `--create-core-tree-case` 能力建立案件；不得手工建立或編輯 `aras-upgrade-case.json`。

### 6. Core Tree 比較

```text
請依正式案件清單執行 Core Tree 比較。

案件根目錄：K:\70.ArasUpgradeCases\芯洲
案件識別：芯洲
操作者：BCO\kenny

先執行唯讀 preflight；只有 preflight 為 Ready 才執行正式比較。
使用新的 comparison attempt，不覆寫既有 attempt。
不得修改三份輸入 Tree、Evidence 或 R38，也不得執行 DB、Package Import 或升級。
```

### 7. 人工更新 review register

```text
請由負責操作人員依 manual-reviews.json 檢視並手動更新：

K:\70.ArasUpgradeCases\芯洲\core-tree\attempts\<attempt-id>\manual-review-register.md

每一筆 review 必須填入合法 Decision、Approver、ISO 8601 Approved at，並標示 Resolved。
不得修改 Review ID、Relative path、Issue code 或 manual-reviews.json。
```

### 8. Review approval／finalization

```text
請對目前 comparison attempt 執行正式 review approval／finalization。

只有所有 review row 均由負責操作人員標示 Resolved 且 checksum 相符時才可繼續。
先使用 --approve-reviews，再使用 --finalize-comparison。
不得覆寫原始 attempt，不得手工建立 approval 或 completion receipt。
```

### 9. 正式 delivery

```text
請執行正式 --build-delivery，
使用已完成的 comparison completion receipt 建立新的 Core Tree A／B／C delivery。

要求：
- delivery output 必須是 core-tree\deliveries 下的新目錄；
- customer、source OOTB、target OOTB 與 Server 規則輸入保持 immutable；
- C 類只依 target path 改用交付檔名，原始 bytes 不轉換；
- 不覆寫既有 delivery；
- 不執行 DB、Package Import、Aras Export 或升級。
```

## 重要停止條件

- Tree、Evidence、正式案件清單或 Server 規則缺失：停止。
- Preflight 為 `Incomplete` 或 `Blocked`：不得建立比較 attempt。
- 有未解決 review：不得 approval、finalization 或 `Completed` delivery。
- 既有 attempt、completion 或 delivery：不得覆寫，必須依正式規則判定是否建立新的唯一輸出。
- `--build-deliver` 是錯誤參數；正確參數為 `--build-delivery`。

## 目前芯洲案件流程結果

截至目前，芯洲案件已完成上述第 1–9 項，正式 Core Tree delivery 已建立。Core Tree delivery 完成不代表 Package／DB 升級完成；兩者仍是獨立工作流。

# Customer-Patch 程序與 Prompt 對照表

本文件供操作人員將目前案件執行紀錄，逐項對照正確的 `.md` 與 Codex Prompt。以下順序適用於 `CUSTOMER_PATCH_COMPARISON`；不包含 OOTB Rule 1，也不執行 Rule 2 Package 適配。

## 固定執行順序

| 項次 | 程序 | 應使用文件 | 主要產出 |
|---|---|---|---|
| 1 | 目錄建立 | [`codex-package-preparation-prompt.md`](codex-package-preparation-prompt.md) | preparation、plan、三份 `patch-support-evidence.request.json` 草稿 |
| 2 | 複製升級對應 Package 輸入 | [`codex-package-preparation-prompt.md`](codex-package-preparation-prompt.md) 的輸入目錄規格 | 三跳 source／target 輸入檔案；不得產生 evidence 或 rule |
| 3 | Customer-Patch 規則發布準備 | [`codex-customer-patch-rule-publication-preparation-prompt.md`](codex-customer-patch-rule-publication-preparation-prompt.md) | approval draft、approval receipt、pending、publication request |
| 4 | 人工明確確認發布 | [`codex-customer-patch-rule-publication-execution-prompt.md`](codex-customer-patch-rule-publication-execution-prompt.md) 的確認區塊 | 具名 `approvedBy` 的發布確認；尚未產生 published rule |
| 5 | 受控規則發布 | [`codex-customer-patch-rule-publication-execution-prompt.md`](codex-customer-patch-rule-publication-execution-prompt.md) | `rule-sets\drafts` 與 `rule-sets\published\<RuleSetId>\00000001.json` |
| 6 | Customer-Patch evidence 登錄 | [`codex-customer-patch-evidence-recording-prompt.md`](codex-customer-patch-evidence-recording-prompt.md) | 每一跳正式 `patch-support-evidence.json` 與 append-only history |
| 7 | Customer-Patch 比較前置驗證 | [`codex-package-comparison-preparation-prompt.md`](codex-package-comparison-preparation-prompt.md) | 每一跳 `Ready` 或 `Blocked`；`Blocked` 不建立 attempt |
| 8 | 開始實際 Customer-Patch Package 比較 | 目前尚無正式 Customer-Patch 比較 CLI；必須等正式 command／Prompt 發布 | 目前不得建立 comparison attempt 或宣稱已開始比較 |

第 3 項與第 5 項不可合併：第 3 項只準備人工核准與發布 request，第 5 項才由受控 CLI 建立 published rule。第 6 項也不會補建任何 `rule-sets` 文件。

## 共同案件參數

```text
案件根目錄：K:\70.ArasUpgradeCases\芯洲
操作者：BCO\kenny
準備目錄：preparation-20260904-001
Customer-Patch Comparison 規則庫：K:\70.ArasUpgradeCases\芯洲\rule-sets
跳點：11.0-sp8-to-11.0-sp15
跳點：11.0-sp15-to-12.0-sp18
跳點：12.0-sp18-to-r38
```

## 各項可貼 Prompt 範例

### 1. 目錄建立

使用 `codex-package-preparation-prompt.md`，並指定：

```text
請以 DIRECTORY_SCAFFOLD_ONLY 模式建立 Customer-Patch Comparison preparation。

案件根目錄：K:\70.ArasUpgradeCases\芯洲
準備目錄名稱：preparation-20260904-001
客戶 Package 基準識別：customer-11sp9
客戶 Package 基準實際版本：11.0 SP9
登錄者：BCO\kenny
跳點：11.0 SP8→11.0 SP15
跳點：11.0 SP15→12.0 SP18
跳點：12.0 SP18→R38
```

此階段不得建立 `rule-sets\published`、正式 evidence 或 history。

### 2. 複製 Package 輸入

```text
請依 preparation plan 將已確認的 Customer Package 基準與各跳 Patch／Support Package 檔案放入指定 input 目錄。

案件根目錄：K:\70.ArasUpgradeCases\芯洲
準備目錄：preparation-20260904-001

只執行目錄與檔案複製；不得比較、解析 AML、建立 evidence、發布規則、修改 plan 或建立 comparison attempt。
完成後只回報三跳 source／target 檔案數與路徑驗證結果。
```

### 3. 規則發布準備

使用 `codex-customer-patch-rule-publication-preparation-prompt.md`，執行正式 CLI：

```text
--prepare-customer-patch-rule-publication <request.json>
```

```text
請準備 Customer-Patch Comparison 共用規則的人工核准與受控發布資料。

案件根目錄：K:\70.ArasUpgradeCases\芯洲
具名人工操作者：BCO\kenny
人工核准結論：同意
```

若 `rule-sets` 不存在，第 3 項 CLI 會先建立；預期建立：

```text
rule-sets\approval-drafts\customer-patch-common-v1-approval-draft.md
rule-sets\approval-drafts\customer-patch-common-v1.approval.json
rule-sets\publication-requests\customer-patch-common-v1.pending.md
rule-sets\publication-requests\customer-patch-common-v1.request.json
```

### 4. 人工明確確認發布

此項不是另一個準備程序；使用規則發布 execution Prompt 的確認區塊：

```text
確認發布 Customer-Patch 共用規則。

案件根目錄：K:\70.ArasUpgradeCases\芯洲
核准人：BCO\kenny
```

確認本身不代表已發布；必須等第 5 項受控 CLI 成功。

### 5. 受控規則發布

繼續使用 `codex-customer-patch-rule-publication-execution-prompt.md`。它必須先驗證 request、receipt、approval draft checksum 與尚未存在 published rule，再執行受控發布。成功產出：

```text
K:\70.ArasUpgradeCases\芯洲\rule-sets\published\<RuleSetId>\00000001.json
```

不可提供或使用舊路徑：

```text
rule-sets\approvals\...
```

正式受控發布的可貼 Prompt：

```text
請執行正式受控 Customer-Patch 規則發布 CLI。

確認發布 Customer-Patch 共用規則。
案件根目錄：K:\70.ArasUpgradeCases\芯洲
核准人：BCO\kenny
使用文件：codex-customer-patch-rule-publication-execution-prompt.md
```

### 6. Customer-Patch evidence 登錄

使用 `codex-customer-patch-evidence-recording-prompt.md`，每一跳單獨執行：

```text
請依 Customer-Patch evidence recording Prompt 登錄單一跳點。

案件根目錄：K:\70.ArasUpgradeCases\芯洲
登錄者：BCO\kenny
跳點識別：11.0-sp8-to-11.0-sp15
```

再依序執行另外兩跳。此項只產生：

```text
<hop>\customer-patch-comparison\evidence\patch-support-evidence.json
```

不會產生 approval、draft、published rule 或 `rule-sets` 子目錄。

### 7. Customer-Patch 比較前置驗證

使用 `codex-package-comparison-preparation-prompt.md`：

```text
請執行 Customer-Patch 比較前置驗證。

案件根目錄：K:\70.ArasUpgradeCases\芯洲
跳點識別：11.0-sp8-to-11.0-sp15
Customer-Patch Comparison 已發布規則儲存位置：K:\70.ArasUpgradeCases\芯洲\rule-sets
執行動作：PREFLIGHT_CUSTOMER_PATCH
```

此項必須同時解析正式 evidence 與 `rule-sets\published\...`。若任一缺少，結果為 `Blocked`；這代表 preflight 已執行但未通過，不是尚未執行。

指定跳點的可貼 Prompt：

```text
請執行 Customer-Patch 比較前置驗證。

案件根目錄：K:\70.ArasUpgradeCases\芯洲
跳點識別：11.0-sp8-to-11.0-sp15
Customer-Patch Comparison 已發布規則儲存位置：K:\70.ArasUpgradeCases\芯洲\rule-sets
執行動作：PREFLIGHT_CUSTOMER_PATCH
```

### 8. 開始實際 Package 比較

目前專案的 Package CLI 僅提供 `--preflight-customer-patch-comparison`，尚未提供實際 Customer-Patch Package 比較 command。因此第 7 項得到 `Ready` 只表示輸入、正式 evidence 與已發布規則均可供比較，不表示已建立 comparison attempt 或已完成比較。

在正式比較 CLI 發布前，使用下列 Prompt 只能進行能力確認，不得自行猜測 command、建立 attempt 或執行比較：

```text
請在 Customer-Patch 比較前置驗證已為 Ready 後，確認目前專案是否提供正式 Customer-Patch Package 比較 CLI。

案件根目錄：K:\70.ArasUpgradeCases\芯洲
跳點識別：11.0-sp8-to-11.0-sp15

若正式比較 command 尚未提供，請回報 NotAvailable 並停止；不得使用舊 Rule 1 command、手工建立 comparison attempt、修改 Package 或宣稱已開始比較。
若正式比較 command 已提供，請只依該 command 的正式 Prompt 與 request schema 執行，並先顯示 command、attempt 輸出目錄與寫入範圍供確認。
```

## 比對檢查表

```text
[ ] 第 1 項只建立 preparation 與 request 草稿
[ ] 第 2 項只放入輸入檔案，未進行比較
[ ] 第 3 項已產生 approval-drafts 與 publication-requests
[ ] 第 4 項已由具名人工確認發布
[ ] 第 5 項已產生 rule-sets\published\<RuleSetId>\00000001.json
[ ] 第 6 項三跳各有正式 patch-support-evidence.json
[ ] 第 7 項 preflight 結果為 Ready
[ ] 第 8 項僅在正式 Customer-Patch 比較 CLI 已發布且獲准後開始
```

若第 5 項未完成，不得把第 7 項的 `ruleStoreRoot` 目錄存在視為規則可用；若第 6 項未完成，也不得把 request 草稿視為正式 evidence。

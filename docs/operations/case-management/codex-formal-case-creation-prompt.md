# Codex Prompt：建立正式 Aras 升級案件

本 Prompt 是所有客戶與版本共用的正式案件建立範本。它使用正式 CLI `--create-core-tree-case` 建立可供 Core Tree preflight／比較讀取的 `aras-upgrade-case.json`。

本 Prompt 建立的是 **Core Tree 工作流**；它與 Package／DB 工作流獨立。只要三份 Core Tree 與其 evidence 已驗證，就可建立 Core Tree 工作流並執行 preflight／比較，不需要等待 Package／DB 升級路徑、Patch、Support 或 SOP。

它與 `DIRECTORY_SCAFFOLD_ONLY` 不同：後者只能建立目錄與非正式範本；本 Prompt 建立的是正式案件清單。正式案件清單只能由受測案件建立能力產生，不得用手工 JSON、PowerShell 或文字編輯器偽造。

## 案件範例資料

在 Codex 載入本文件後，先提供或選定一組案件資料。不要保留 `<...>` 佔位符。

```text
客戶代號：<CUSTOMER_CODE>
案件目錄名稱：<CASE_DIRECTORY_NAME>
案件根目錄：<CASE_ROOT，例如 K:\70.ArasUpgradeCases>
來源版本：<SOURCE_VERSION，例如 11.0 SP9>
來源版本短名稱：<SOURCE_SLUG，例如 11sp9>
最終版本：<TARGET_VERSION，例如 R38>
最終版本短名稱：<TARGET_SLUG，例如 r38>
操作者：<DOMAIN\USER>
```

Core Tree 設定由下列規則推導，不必每次手動填寫所有路徑：

```text
案件目錄：<CASE_ROOT>\<CASE_DIRECTORY_NAME>
Customer input：customer-<SOURCE_SLUG>
Source OOTB input：ootb-<SOURCE_SLUG>
Target OOTB input：ootb-<TARGET_SLUG>
比較名稱：customer-<SOURCE_SLUG>-To-ootb-<TARGET_SLUG>
```

### 芯洲 11SP9 → R38 範例

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

## 在 Codex 對話框的輸入方式

1. 附加或讀取本 Markdown。
2. 在同一則訊息貼上其中一組案件資料，或提供新的實際值。
3. 接著只需輸入：

```text
案件建立
```

`案件建立` 的意思是：沿用本文件與同一 task 中最後一組已明確提供的案件資料，推導 Core Tree 設定並呼叫正式案件建立能力。若同一 task 尚未有完整案件資料，Codex 必須要求資料，不得自行選擇範例或其他客戶。

## Package／DB 工作流（後續獨立建立）

Package／DB 工作流在後續要開始時，才需要操作人員提供並確認：

- 連續的 Package Import 跳點；
- 每一跳的來源／目標版本；
- 每一跳的 Aras 官方 Patch／Support 位置；
- 路徑確認參考與建立確認資訊。

不得將來源版本直接填成目標版本的單一跳點，除非操作人員已提供相符的正式 SOP 與 Support 證據。這些資料不是本次 Core Tree 工作流建立的必要條件。

## 共用 Codex Prompt

```text
請使用本專案的正式案件建立能力建立 Core Tree 工作流案件，不要手工建立或編輯 aras-upgrade-case.json 或 .orchestrator\history.jsonl。

請沿用本文件與本 task 最後一組明確提供的案件資料，推導下列 Core Tree 設定：

- customerInputId：customer-<SOURCE_SLUG>
- sourceOotbInputId：ootb-<SOURCE_SLUG>
- targetOotbInputId：ootb-<TARGET_SLUG>
- customerTreePath：core-tree\inputs\customer-<SOURCE_SLUG>\tree
- customerEvidencePath：core-tree\inputs\customer-<SOURCE_SLUG>\evidence
- sourceOotbTreePath：core-tree\inputs\ootb-<SOURCE_SLUG>\tree
- sourceOotbEvidencePath：core-tree\inputs\ootb-<SOURCE_SLUG>\evidence
- targetOotbTreePath：core-tree\inputs\ootb-<TARGET_SLUG>\tree
- targetOotbEvidencePath：core-tree\inputs\ootb-<TARGET_SLUG>\evidence
- comparisonName：customer-<SOURCE_SLUG>-To-ootb-<TARGET_SLUG>

執行前：
1. 確認案件根目錄與三份 Core Tree Evidence 已存在。
2. 確認正式案件建立 UI、CLI 或 command 可呼叫。
3. 驗證三份輸入的版本證據、`Innovator\Client`／`Innovator\Server` 結構與輸入／輸出隔離。
4. 目標已有 aras-upgrade-case.json 或 .orchestrator\history.jsonl 時停止，不覆寫。

只有以上條件全數通過時，才使用正式 CLI `ArasUpgradeOrchestrator.CoreTree.Cli.dll --create-core-tree-case <request.json>` 產生新的案件 GUID、aras-upgrade-case.json 與必要的 `.orchestrator` 目錄。建立前與建立後都不得存取 `CurrentRoute`，不得要求、建立或推測 Package／DB `routes`；`history.jsonl` 會在首次正式比較時由比較 command 自行追加，首次 preflight 前不存在是預期狀態。

若案件資料、三份已驗證 Core Tree 輸入或正式 Core Tree 案件建立入口任一缺失，停止且回報「正式 Core Tree 案件建立待補資料」；仍不得手工建立 JSON。不要執行 Core Tree 比較、DB、Package Import、Aras Export 或升級。
```

## 正式 JSON 的共用結構

建立能力會使用下列邏輯產生 `coreTreeComparison`。這是輸入結構，不能直接保存為完整案件清單：

```json
{
  "customerInputId": "customer-<SOURCE_SLUG>",
  "sourceOotbInputId": "ootb-<SOURCE_SLUG>",
  "targetOotbInputId": "ootb-<TARGET_SLUG>",
  "customerTreePath": "core-tree/inputs/customer-<SOURCE_SLUG>/tree",
  "customerEvidencePath": "core-tree/inputs/customer-<SOURCE_SLUG>/evidence",
  "sourceOotbTreePath": "core-tree/inputs/ootb-<SOURCE_SLUG>/tree",
  "sourceOotbEvidencePath": "core-tree/inputs/ootb-<SOURCE_SLUG>/evidence",
  "targetOotbTreePath": "core-tree/inputs/ootb-<TARGET_SLUG>/tree",
  "targetOotbEvidencePath": "core-tree/inputs/ootb-<TARGET_SLUG>/evidence",
  "comparisonName": "customer-<SOURCE_SLUG>-To-ootb-<TARGET_SLUG>"
}
```

完整正式 Core Tree 案件清單仍必須由建立能力補入：`schemaVersion`、新案件 GUID、`createdAt`、Core Tree 工作流識別與 `artifactLocations`。Package／DB 工作流尚未建立時，`currentRouteVersion` 與 `routes` 不得被要求、推測或手工補齊；未來建立該工作流時，才由其正式能力寫入已驗證的路徑。

# 討論 Aras Innovator 升級需求

- 整理日期：2026-08-09
- 專案：Aras Upgrade Orchestrator
- 文件用途：保存本次對話形成的需求、決策、Skill 架構與實作結果，供後續需求訪談、同事交接及維護使用。
- 適用範圍：僅限目前的 Aras Innovator 升級專案，不與其他專案混用。

## 1. 專案目標

主要目標是將 Aras Innovator 升級作業整理成可交接、可逐步補充且可由 AI 執行的 Skill 體系。

同事不需要預先知道底層工具一定使用 C#、Python 或其他語言，只需要使用明確的 Skill 指令。AI 應依 Skill 契約尋找既有工具，或在必要時產生符合契約的程式，以完成單一任務。

Skill 與相關文件原則上使用繁體中文；程式識別碼、檔名、路徑、API、XML／AML 元素、Aras Innovator 術語及能以一句英文準確表達的專業用語保留原文。

## 2. Aras Innovator 整體升級程序

客戶提供目前版本的資料如下：

- Aras Innovator DB。
- 客戶目前版本的 Core Tree。

公司內部利用上述資料建立客戶模擬環境，進行升級準備及演練。主要程序為：

1. 建立客戶目前版本的模擬環境。
2. 產生該升級階段所需的客戶 Package。
3. 將客戶 Package 與對應 OOTB／升級工具 Package 比較。
4. 依比較結果調整升級工具內的 Package，形成該跳點的適配 Package。
5. 必要時將升級工具原始 Package 備份到其他目錄。
6. 正式執行時直接使用已調整完成的 Aras Innovator 升級工具。
7. 依升級路徑逐一完成版本跳點。
8. 比較客戶 Core Tree、來源版 OOTB Core Tree 與最終版 OOTB Core Tree。
9. 將 Core Tree 比較產出交給另一單位進行 R38 客製內容調整。

## 3. 升級版本跳點

升級必須依 Aras Innovator 支援的版本跳點逐段完成，不能直接省略必要中間版本。

已討論的範例如下：

- `12SP9 → 12SP18 → R38`
- `11SP5 → 11SP15 → 12SP18 → R38`

不同版本跳點對應不同的 Aras Innovator 升級工具目錄。每一跳點的 Package 比較、適配 Package 及升級執行應保留獨立結果與證據。

## 4. 客戶 Package 產生方式

對話中原有的 `pp` 為筆誤，沒有特殊意義。

客戶 Package 由公司內部產生，程序為：

1. 先導出目前 DB 中的 Package 資料。
2. 使用 SQL command 刪除 Package 相關 Table 的資料。
3. 從與客戶相同版本的 Innovator OOTB 環境導出對應 Table 資料。
4. 將 OOTB Table 資料導入客戶 DB。
5. 使用 Aras Export 工具導出客戶 Aras Package。

此程序可用於取得不同升級階段需要比較的客戶 Package。

Package 比較只需比較公司產生的客戶 Package 與升級所需的對應 Package，不要求在比較後再次重新導出客戶 Package。

## 5. Package 比較與適配 Package

Package 比較規則曾在另一項工作中分析，應另外整理成明確規則，不在 Core Tree Skill 中混合處理。

建議的任務關係為：

1. 完成客戶 Package 產生。
2. 依升級路徑，為每個跳點產生 Package 比較子任務。
3. 完成該跳點的 Package 比較。
4. 產生該跳點的適配 Package。
5. 通過安全與證據關卡後，執行單一升級跳點。
6. 完成後再進入下一跳點。

正式執行使用調整完成的 Aras 升級工具內容；原始 Package 如需保存，應備份到其他目錄，不應在正式執行時再臨時替換。

## 6. Core Tree 比較輸入

Core Tree 比較使用三份資料：

1. 客戶來源版本 Core Tree，例如客戶 `12SP9`。
2. 相同來源版本的 OOTB Core Tree，例如 OOTB `12SP9`。
3. 目標版本的 OOTB Core Tree，例如 OOTB `R38`。

每份輸入都必須具備可追溯的版本證據，並包含：

- `Innovator/Client`
- `Innovator/Server`

三份輸入必須視為 immutable inputs。比較程序不得直接修改客戶、來源 OOTB 或目標 OOTB Core Tree。

## 7. Client 與 Server 比較原則

### 7.1 Client

Client 主要比較程式及文字內容，例如：

- `.js`
- `.ts`
- `.tsx`
- `.htm`
- `.html`
- `.cshtml`
- `.xml`

文字比較只能忽略明確允許的差異，例如 CRLF／LF 與相同編碼的 UTF BOM。不得因解碼後文字相同，就將 UTF-8 與 UTF-16 判定為相同。

### 7.2 Server

Server 原則上只做檔案二進位比較，例如 `Server/bin` 下的檔案。

只有固定規則明確列出的檔案採文字比較，例如：

- `Server/method-config.xml`

Server 文字比較規則必須具備版本及 Checksum。規則內的路徑必須是安全且正規的 `Server/...` 相對路徑，不可接受 rooted path、`.`、`..`、空片段、重複分隔符號或反斜線繞過。

## 8. Core Tree A／B／C 分類

### 8.1 A 類：客戶新增檔案

來源版 OOTB 不存在，但客戶來源版本存在的檔案。

比較方式：

```text
客戶來源版本 vs. 相同版本 OOTB
```

交付內容以客戶新增檔案為主。

### 8.2 B 類：客戶修改，但目標版已不存在

客戶來源版本與相同版本 OOTB 有差異，表示客戶曾修改；但該項目在目標版 OOTB 中不存在。

交付目錄包含：

```text
B/
├─ 客戶來源版本/
└─ OOTB來源版本/
```

### 8.3 C 類：客戶與目標版都有變更

客戶來源版本與來源版 OOTB 有差異，而且目標版 OOTB 仍存在對應檔案。

交付目錄包含：

```text
C/
├─ 客戶來源版本/
├─ OOTB來源版本/
└─ OOTB目標版本/
```

目前 Core Tree 工作只需要建立比較及分類交付目錄。交付目錄會提供給另一單位，進行 R38 客製內容調整；本次 Skill 不負責自動合併或修改 R38 Core Tree。

## 9. 副檔名演進與邏輯配對

Core Tree 比較不能只依完全相同檔名配對，還要辨識目標版本的副檔名演進。

目前需要支援的演進候選包括：

- `.htm → .html`
- `.htm → .cshtml`
- `.html → .cshtml`
- `.js → .ts`
- `.js → .tsx`

判斷方式不綁定特定版本對照表。比較時應先觀察目標版是否存在新副檔名，再判斷來源檔案是否為允許的舊格式。

邏輯配對限制如下：

- 只在相同相對目錄中搜尋。
- 必須具有相同主檔名。
- 不可跨目錄猜測。
- 找到多個候選時必須轉人工確認。
- 不可由 AI 任意選擇其中一個候選。

C 類交付以目標版本的副檔名為準。例如來源客戶檔案為 `.js`，目標版唯一對應為 `.ts`，則 C 類的客戶來源版本副本在交付目錄中使用 `.ts` 檔名，但內容仍保留客戶來源檔案內容。

## 10. 任務拆分原則

任務存在順序相依，但部分能力可以單獨執行：

- Core Tree 輸入驗證可以單獨執行。
- 指定兩個檔案的內容比較可以單獨執行。
- 指定來源檔案的邏輯配對可以單獨執行。
- Core Tree A／B／C 分類需要已驗證輸入。
- Core Tree 交付目錄建立需要已完成的分類結果。
- 客戶 Package 比較必須在客戶 Package 產生完成後進行。
- 每個 Package 升級跳點可以建立獨立子任務。
- 單一升級跳點只能在該跳點適配 Package 完成後執行。

每個 Skill 應對應一個明確、穩定且可獨立維護的業務能力，不以特定 `.cs` 檔案或特定程式語言作為 Skill 邊界。

## 11. Core Tree Skill 架構

父 Skill：

- `aras-compare-core-tree`：負責完整 Core Tree 比較流程協調，以及將單一能力要求路由到正確的細項 Skill。

本次新增五個細項 Skill：

| Skill | 單一責任 |
|---|---|
| `aras-validate-core-tree-inputs` | 驗證三份 Core Tree、版本證據、必要結構、Server Rule 及輸入輸出隔離。 |
| `aras-compare-core-tree-content` | 依 Client／Server 固定規則比較指定的兩個檔案。 |
| `aras-resolve-core-tree-file-mappings` | 解析來源檔案在目標版中的唯一邏輯對應及副檔名演進。 |
| `aras-classify-core-tree-differences` | 掃描三份 Core Tree 並產生 A／B／C、人工確認、錯誤及阻擋結果。 |
| `aras-build-core-tree-delivery` | 根據已驗證分類建立新的不可覆寫交付目錄與完成狀態。 |

分類結果為 `Blocked` 時，父 Skill 仍須路由至交付 Skill，建立可供診斷及交接的 `Incomplete` 產出；但不得繼續後續升級作業。只有零錯誤且零人工確認時，才能建立 `Completed`。

## 12. Skill 套件目錄結構

以 `aras-validate-core-tree-inputs` 為例：

```text
aras-validate-core-tree-inputs/
├─ SKILL.md
├─ agents/
│  └─ openai.yaml
├─ references/
│  ├─ input-contract.md
│  ├─ output-contract.md
│  ├─ rules.md
│  └─ error-and-stop-conditions.md
└─ assets/
   └─ acceptance-cases/
      ├─ valid-inputs/
      ├─ version-mismatch/
      ├─ missing-structure/
      ├─ overlapping-output/
      └─ rule-checksum-mismatch/
```

各部分關係如下：

- `SKILL.md`：Skill 觸發條件、主要程序、責任邊界及 references 導覽。
- `agents/openai.yaml`：Skill 清單顯示名稱、簡短說明及預設提示；不是另一個執行代理。
- `references/input-contract.md`：輸入欄位與證據格式。
- `references/output-contract.md`：輸出 envelope、狀態、訊息及證據格式。
- `references/rules.md`：詳細業務與安全規則。
- `references/error-and-stop-conditions.md`：穩定錯誤代碼及停止條件。
- `assets/acceptance-cases/`：以 `input.json` 和 `expected/result.json` 保存可重複驗收案例。

載入關係為：

```text
Skill metadata
    → 判斷是否觸發
SKILL.md
    → 控制主要程序
references
    → 提供詳細契約
acceptance-cases
    → 驗證實作是否符合契約
```

## 13. Skill 與 C# 參考實作的定位

正式規格來源是 Skill 契約及驗收案例，不是特定程式語言。

目前五個 Skill 有下列已驗證 C# 參考實作：

| 業務能力 | Skill | 現行已驗證參考實作 | 程式位置 |
|---|---|---|---|
| 驗證 Core Tree 輸入 | `aras-validate-core-tree-inputs` | `CoreTreeInputValidator` | `src/ArasUpgradeOrchestrator.Core/CoreTrees/CoreTreeInputValidator.cs` |
| 比較兩個 Core Tree 檔案 | `aras-compare-core-tree-content` | `CoreTreeContentComparer` | `src/ArasUpgradeOrchestrator.Core/CoreTrees/CoreTreeContentComparer.cs` |
| 解析邏輯檔案對應 | `aras-resolve-core-tree-file-mappings` | `CoreTreeLogicalPathResolver` | `src/ArasUpgradeOrchestrator.Core/CoreTrees/CoreTreeLogicalPathResolver.cs` |
| 產生 A／B／C 分類 | `aras-classify-core-tree-differences` | `CoreTreeComparisonEngine` | `src/ArasUpgradeOrchestrator.Core/CoreTrees/CoreTreeComparisonEngine.cs` |
| 建立交付目錄 | `aras-build-core-tree-delivery` | `CoreTreeComparisonBuilder` | `src/ArasUpgradeOrchestrator.Core/CoreTrees/CoreTreeComparisonBuilder.cs` |

共用支援元件包括：

- `CoreTreeModels.cs`：Core Tree 輸入、輸出及分類資料模型。
- `CoreTreePathOrdering.cs`：穩定排序規則。
- `DirectoryLeaseManager.cs`：防止輸出目錄重疊執行及覆寫。

未來可以改用 Python 或其他語言。替代實作必須遵守相同輸入輸出、穩定錯誤代碼、安全限制、不可變輸入要求及驗收案例，才能視為符合 Skill。

## 14. 驗收與證據

本次完成後的狀態：

- 完整測試：84/84 通過。
- Core Tree fixture pairs：34 組。
- Authoritative implementation：`9c3a508a5b36fdde3d122ef212774245410d33b1`。
- Immutable evidence commit：`b703dd9e5861d2477eae37b417cfad78d65b992c`。
- 舊版 `06a95761d8c210efb5a8271793ac36f602274a94` 的 83/83、33 組結果保留為 superseded history。

最終審查曾發現並完成修正：

- Server Rule 路徑安全驗證不足。
- `Blocked` 分類未建立必要的 `Incomplete` 診斷交付。
- UTF-8 與 UTF-16 相同可見文字被誤判為相同。
- null、空字串或空白輸入根目錄未回傳穩定錯誤。
- 排序缺少 ordinal 次要排序。
- Issue 歷史敘述與目前 `resolved` 狀態不一致。
- 新實作完成後，authoritative evidence 與測試／fixture 計數未同步。

以上問題修正後，最終整體審查核准。

## 15. Git 交付結果

本次實作已以 fast-forward 合併到本機 `main`：

- `main` HEAD：`b703dd9e5861d2477eae37b417cfad78d65b992c`
- 未建立額外的新分支。
- 暫用功能分支已刪除。
- 暫用 worktree 已移除。
- 未推送遠端。
- 合併後重新驗證：建置 0 警告、0 錯誤，84/84 測試通過，format 與 diff check 通過。
- 當時本機 `main` 領先 `origin/main` 31 個提交。
- 主目錄原有未追蹤 `.superpowers/` 保留不動。

## 16. 交付成果與報告定位

整體 Aras Innovator 升級最終成果可包括：

- R38 DB。
- R38 Core Tree。
- 各跳點適配 Package。
- 執行紀錄及不可覆寫證據。
- 差異摘要或差異報告。
- 驗證摘要或驗證報告。

差異報告與驗證報告本身不應阻礙正常升級；只有當報告揭露必要輸入缺漏、錯誤、人工確認未解除、Checksum 不符或其他安全關卡未通過時，才應阻擋完成。

報告格式、必要欄位及是否列為正式交付物，仍需在最終交付 Skill 中另外明確定義。

## 17. 本次未納入範圍及後續工作

本次討論與實作未包含：

- 自動合併或直接修改 R38 Core Tree。
- 由 Core Tree Skill 自行完成另一單位負責的客製內容調整。
- Package 比較規則的完整重新定義。
- 每個升級跳點的正式外部工具操作。
- 正式 DB 連線及 DB 還原操作。
- 最終交付組裝 Skill 的完整規格。

建議後續依單一任務原則繼續補充：

1. 客戶 Package 產生程序與一次性安全關卡。
2. Package 比較 Rule 1／Rule 2 的正式規則與版本例外。
3. 各升級跳點適配 Package。
4. 單一升級跳點執行及 Rollback 證據。
5. R38 DB、R38 Core Tree、Package、執行紀錄與報告的最終交付組裝。

## 18. 重要決策摘要

- Skill 依穩定業務能力拆分，不依 `.cs` 檔案拆分。
- Skill 是規格與交接入口；程式是可替換的實作。
- AI 不可猜測多候選邏輯配對。
- Core Tree 三份輸入不可修改。
- 每次交付嘗試建立新目錄，不覆寫舊結果。
- 錯誤或人工確認未解除時只能產生 `Incomplete`。
- 只有零錯誤且零人工確認才能產生 `Completed`。
- Core Tree 比較交付與 R38 客製內容合併是不同責任。
- Package 產生、Package 比較、適配 Package 及單一跳點執行應為不同 Skill 或子任務。
- 所有重要判斷都應保留版本、Checksum、輸入識別及執行證據。

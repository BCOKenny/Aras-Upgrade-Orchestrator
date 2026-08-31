---
name: aras-manage-upgrade-case
description: 建立、開啟、檢查及更新可追溯的 Aras Innovator 升級案件，管理案件清單、版本化升級路徑、任務圖、執行歷程、中斷狀態、安全重試資格與受控執行關卡。當 Codex 需要處理客戶升級目錄中的案件識別、跳點路徑、任務相依、執行嘗試、更正紀錄、目錄鎖或安全白名單判定時使用；不負責 AML、Package、Core Tree 內容比較或實際 DB／Aras 工具操作。
---

# Aras 升級案件管理

## 目標

使用正式受測核心管理單機單人升級案件。把案件清單視為可驗證輸入，把執行歷程視為只追加的已發生事實；不要在 Skill 內重寫核心判定。

## 開始前

1. 讀取儲存庫根層 `AGENTS.md`、`CONTEXT.md` 及相關 ADR。
2. 讀取 `.scratch/aras-upgrade-orchestrator/spec.md` 第 4、5、6、15、18 節。
3. 讀取主 Skill 的 `references/project-facts.md` 與 `references/terminology.md`。
4. 需要操作或擴充正式程式能力時，讀取 `references/core-capabilities.md`。
5. 確認使用者提供的案件根目錄；不得從資料夾名稱推測案件識別或版本。

## 選擇案件動作

### `VALIDATE_ONLY` 例外分支

若使用者明確指定 `VALIDATE_ONLY`，本次要求是參數與路徑驗證，不是建立、開啟或檢查正式案件。此分支必須優先於正式案件模型規則：

- 只驗證客戶、案件名稱、來源／目標版本、短名稱、案件目標路徑及外部根目錄白名單格式；
- 不載入或要求 `CaseManifest`、升級跳點、`Support`、SOP、Patch、Build、DB 備份或正式案件歷程；
- 案件目錄、案件清單、歷程與鎖不存在時，只列為資訊，不得回報 `Blocked`；
- 驗證通過時回報 `VALIDATION_ONLY: PASS`，只有格式或路徑錯誤才回報 `VALIDATION_ONLY: INVALID`；
- 驗證輸出後立即結束，不得進入建立案件、正式案件檢查或受控執行流程。

### `DIRECTORY_SCAFFOLD_ONLY` 例外分支

若使用者明確指定 `DIRECTORY_SCAFFOLD_ONLY`，本次只建立已授權案件根目錄下的目錄與非正式範本：

- 不載入或建立正式 `CaseManifest`、`aras-upgrade-case.json` 或 `.orchestrator\history.jsonl`；
- 不要求連續升級跳點、正式 SOP、Patch、Support、Build 或 DB 證據；
- `DIRECTORY_SCAFFOLD_ONLY` 本身是受控的正式目錄建立動作；不得要求另一個 UI／CLI／command/action 作為前置條件；
- 仍須檢查使用者明確指定的固定外部根目錄、執行權限、目標是否重疊及既有資料，禁止覆寫或刪除；
- 若以本地腳本執行，Markdown 與腳本內容必須分離；送往執行器的內容不得使用反引號、JavaScript template literal 或 `String.raw`；
- Windows PowerShell 腳本須以 UTF-8 with BOM 寫入、來源限 ASCII；中文案件名稱以 Unicode code point 在執行期還原。所有預定輸出須先完成零寫入預檢；失敗產物不得自動刪除或重試；
- 若沒有外部路徑寫入權限，回報阻擋並停止，不改寫到專案目錄。

- **建立 Core Tree 工作流：**驗證案件識別、客戶代號、來源與目標版本、三份 `tree`、三份 evidence 與 `coreTreeComparison` 設定，再呼叫正式 `--create-core-tree-case` 建立能力。不得讀取 `CurrentRoute`，也不得要求或推測 Package／DB 跳點、Patch、Support 或 SOP。建立成功的 Core Tree-only 案件必須有 `routes: []` 與 `currentRouteVersion: 0`；首次比較前不建立 `.orchestrator\history.jsonl`。
- **建立 Package／DB 工作流：**驗證案件識別、客戶代號、來源與目標版本、連續跳點及各跳點 `Support` 登錄資料，再呼叫正式 Package／DB 路徑建立能力。不得以 Core Tree 結果取代這些條件。
- **開啟或檢查案件：**讀取根層 `aras-upgrade-case.json` 及 `.orchestrator/history.jsonl`，由正式投影能力分別重建 Core Tree 與 Package／DB 工作流的任務及狀態。若 `coreTreeComparison` 存在而 `routes` 為空，僅投影 Core Tree 工作流；不得存取 `CurrentRoute`。此情況下歷程檔不存在時回報「尚無 Core Tree 執行嘗試」，不得將它當成案件無效或比較前置阻擋。
- **變更升級路徑：**只適用 Package／DB 工作流；建立更高版本的新路徑，不得修改或刪除既有路徑版本。任一跳點開始後尤其不得把新路徑偽裝成原版本。
- **檢查下一步：**分別顯示 Core Tree 的輸入證據／preflight／比較關卡，以及 Package／DB 的跳點 Package 子任務、跳點執行、人工登入驗證及 DB 備份關卡；兩條工作流沒有相互前置相依。
- **恢復中斷：**只把沒有終止事件的執行嘗試標記為中斷，不自動續跑。
- **建立重試：**只接受已驗證 Idempotency、已完成 Rollback 或已回到 Runbook 指定檢查點的證據。
- **受控執行：**先核對快照與動作，再執行三級安全判定、單次確認及工作目錄鎖；任何必要條件不足都直接阻擋。
- **更正資料：**對執行歷程追加指向原事件的更正紀錄，不修改或刪除原事件。

若目前沒有可呼叫的正式 UI、CLI 或 command/action，停止在相對應工作流的介面邊界並回報缺少的執行面；不得手工改寫案件清單或 `history.jsonl` 來模擬正式能力。Core Tree 建立入口缺失時，不得要求操作人員先提供 Package／DB 資料作為替代。

## 固定安全責任

1. 案件清單缺失、格式不支援、案件識別不符或路徑版本不連續時阻擋受控動作。
2. 已發生歷程只追加；不得壓縮、排序重寫、刪除或以最新狀態覆蓋原事件。
3. 執行快照必須綁定任務、動作識別與版本、目標、輸入、工具版本及適用 Checksum。
4. 確認只對相同快照有效；必要條件不足不能用人工確認繞過。
5. 寫入目錄相同或上下層重疊時阻擋後開始者；DB 跳點不得平行。
6. 預設外部執行器必須保持阻擋；沒有明確授權不得連接 DB、啟動 Aras 工具、修改正式 Package、Core Tree 或升級目錄。
7. 不把 AI 建議當作安全放行、路徑決策、完成證據或解鎖依據。

## 輸出

每次回報至少包含：

- 案件識別、來源與目標版本、目前路徑版本；
- 目前任務與最後執行嘗試狀態；
- 已通過及未通過的關卡；
- 使用的正式能力或尚缺的 command/action；
- 新增的歷程／更正／確認證據識別；
- 阻擋原因與下一個安全動作。

## 責任邊界

將下列工作交回主 `aras-innovator-upgrade` 路由：

- 客戶 Package 一次性產生；
- Rule 1／Rule 2 與 AML 比較；
- 規則集草稿、驗證及發布；
- Core Tree 比較及分類；
- 實際跳點協調與最終交付。

不得為上述工作在本 Skill 內建立臨時替代流程。

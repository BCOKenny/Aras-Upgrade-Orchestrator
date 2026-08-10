# 釐清 Aras 升級需求－對話摘要

> 文件用途：保存目前專案對話中已確認的需求、限制、術語及實作方向。
>
> 文件性質：需求與架構摘要，不代表已完成程式實作；正式規格仍以專案內 `spec.md`、`CONTEXT.md`、`project-facts.md` 及相關標準文件為準。

## 1. 專案目的與目前工作模式

本專案建立「Aras Innovator 升級協調工具」，協助公司重建客戶環境、準備跨版本升級 Package、比較 Core Tree、受控追蹤多跳點升級，並產生可交接的最終交付物。

第一版採以下模式：

- 單機 Windows 桌面工具。
- 單人案件模式，優先確保穩定性與資料正確性。
- 受控執行模式：安全且已核准的動作可由工具執行；高風險操作須由操作人員確認。
- 核心流程不依賴 Codex／AI；AI 只能提供解釋、摘要或建議。
- 客戶案件、工作副本、產出及執行紀錄保存在各自的本機升級目錄，不互相影響。

多人共享案件、即時協作、角色簽核及中央服務列入未來階段，不列入第一版必要功能。

## 2. 已確認的升級路徑與任務關係

升級路徑依原廠文件及操作人員確認，可能包含：

- `12SP9 → 12SP18 → R38`
- `11SP5 → 11SP15 → 12SP18 → R38`

任務關係如下：

- 「Package 比較／產生升級 Package」是父任務。
- 每個版本區間的 Package 準備是「跳點 Package 子任務」，可分開或平行準備。
- 「跳點執行」是實際 DB 從來源版本升級到下一版本的執行節點，不是 Package 子任務。
- 跳點執行必須等待對應的正式適配 Package 完成。
- DB 跳點執行依升級路徑順序進行，不可平行執行。
- Core Tree 比較是獨立任務，可與 Package 準備及 DB 升級平行進行；最終交付仍須等待 Core Tree 完成。

## 3. 受控執行與不可變歷程

### 3.1 安全與授權

- 自動執行採安全白名單。
- 白名單須包含動作版本、目標、輸入限制與前置條件。
- 不在白名單內、超出範圍或無法安全判定的動作，進入單人確認關卡或直接阻擋。
- 第一版採三級安全判定：白名單內可自動執行、重要狀態變更須單次確認、條件不足時阻擋。
- AI 不得產生、修改或自由組合待執行 SQL，不得解除安全阻擋或發布規則。

### 3.2 歷程與重試

- 任務本身保持不變，每次實際運行建立獨立的「執行嘗試」。
- 執行嘗試保存當次輸入、參數、工具／規則版本、結果與證據。
- 已發生的執行歷程不得覆寫或刪除。
- 發現錯誤時新增「更正紀錄」，指向原始歷程並保留原始內容。
- 中斷後不得自動續跑；需由操作人員檢查狀態後建立新的執行嘗試。
- 只有確認 Idempotency、完成 Rollback，或回到指定檢查點後，才可安全重試。

### 3.3 目錄鎖與一次性流程

- 可能寫入相同或重疊目錄的動作須由固定規則阻擋平行執行。
- 不同跳點的獨立工作目錄可平行準備。
- 客戶 Package 產生流程在首次 DB 變更前由工具固定規則鎖定。
- Package 流程失敗時，由操作人員手動將 DB 還原至最後備份點並提交證據；工具不自行還原或自行解鎖。
- 鎖定只有在固定規則確認相符的 Rollback 證據後，才可標記為 `RolledBack`。

## 4. 客戶 Package 一次性產生

流程由操作人員與工具共同完成：

1. 客戶提供來源版本 DB 與 Core Tree，公司重建客戶環境。
2. 工具在 Package 相關 Table 變更前建立原始 Package 備份。
3. 由固定規則鎖定一次性流程。
4. 執行預先核准、版本固定且 Checksum 相符的 SQL Script，刪除 Package 相關 Table。
5. 從相同版本 OOTB 環境導出相關 Table 並匯入客戶 DB。
6. 操作人員使用相應版本的 Aras Export 工具人工導出客戶 Package。
7. 若全選導出因客戶環境異常而中止，操作人員取消異常項目後重新導出；每次操作都建立獨立執行嘗試。
8. 所有導出排除項目須記錄原因及處置；未處置完成時不得形成可用的客戶 Package 基準。

原始 Package 備份第一版只保存，不做比較或修復；用途是未來追查客製時刪除原廠項目所造成的異常。

## 5. OOTB 跳點差異與 Package 適配

### 5.1 Rule 1：OOTB 跳點差異

- 以來源版本 OOTB Package 與目標版本 OOTB Package 產生跳點差異。
- 原始 OOTB Package 是不可變輸入。
- 只在工作副本中直接覆寫來源與目的 XML，產生 `SourceDiff` 與 `TargetDiff`。
- 產出完成標記、摘要及單一 ZIP Checksum，供未來案件驗證重用。
- 已完成的 OOTB 跳點差異可人工備份，放入新的客戶升級目錄；不得只依空目錄或資料夾名稱判定完成。
- 目的在於縮小後續 Rule 2 比較量，例如先完成 `OOTB(1209) → OOTB(1218)`，未來再以該差異搭配客戶來源 Package。

### 5.2 Rule 2：正式適配 Package

- 輸入為不可變的客戶 Package 基準及已驗證的 Rule 1 `TargetDiff`。
- 先備份客戶升級工具原始 `Solutions` Package。
- 建立來源端與目的端工作副本，再依已發布規則直接覆寫 XML。
- 目的端最終結果位於客戶專用升級工具的 `Solutions` 目錄，供跳點執行使用。
- 來源端工作副本只供比較與追查，不作為升級工具執行輸入。
- 無法可靠配對、多候選、規則衝突或錯誤項目須列入人工確認；未處置前阻擋正式適配 Package 及跳點執行。
- 非 XML 檔案不比較、不複製、不刪除、不覆寫，保留各自原本目錄。
- 既有「客戶升級工具封存版本」代表實際執行的整套版本，第一版不另行重複封裝；原始 `Solutions` 備份與封存版本用途不同。

## 6. AML 與比對規則

Package 必須遵守 `docs/standards/AML_Structure_and_Traversal_Standard.md`，不得把 AML 當成一般 XML：

- 區分 AML Root、Item、Scalar Property、Item Property、Relationships Container、Relationship Item。
- 使用遞迴處理，不假設固定深度。
- XML 檔案先依 Package 根目錄下的相對路徑配對，不跨目錄猜測搬移。
- Item 配對使用專案內版本化的 Package CompareKey 規則。
- Key 缺失、重複或多候選時不得猜測，必須轉人工確認並輸出 AML Path。
- 規則草稿通過驗證並發布成新規則集版本後，才能供新的執行嘗試使用。
- 共同準則可適用於一般情況；不符時另建指定來源版本與目標版本的版本例外規則。
- 本次案件裁決可以作為候選規則，但不能由「套用本次裁決」直接變成全域規則。
- 不執行逐 XML Checksum 快照；第一版保存精簡比較摘要，不影響升級程序，只可能影響效能。

## 7. Core Tree 比較

Core Tree 只負責比較與分類輸出，不合併或修改 R38 Core Tree；產出交由另一單位調整。

### 7.1 輸入

三份輸入必須驗證版本與目錄：

1. 客戶來源版本 Core Tree。
2. 相同來源版本 OOTB Core Tree。
3. 目標 R38 OOTB Core Tree。

範圍為 `Innovator\Client` 與 `Innovator\Server`。

### 7.2 比較與配對

- Client 主要進行程式內容比較，例如 `js`、`ts`、`tsx`、`html`、`cshtml`、`htm`、`xml` 等。
- Server 預設做檔案層級比較；如 `method-config.xml` 等明確列入規則集的檔案才做文字內容比較。
- 非文字檔先比檔案大小；大小不同即判定不同，大小相同再做完整串流比較，不抽樣、不依修改時間判定相同。
- 必須在同一相對目錄比較。
- 依 R38 實際檔案反查副檔名演進：`htm → html`、`htm → cshtml`、`html → cshtml`、`js → ts`、`js → tsx`。
- 多個候選不得猜測，列入待人工確認。
- 交付複本在需要時使用 R38 檔名／副檔名，但內容不轉換。

### 7.3 分類

- A：客戶新增檔案。
- B：客戶修改但 R38 不存在對應邏輯檔案，包含客戶來源版及來源 OOTB。
- C：客戶修改且 R38 存在唯一對應邏輯檔案，包含客戶來源版、來源 OOTB 及 R38 OOTB。
- 保留相對目錄。
- 失敗、中斷或人工確認未完成時標記 `Incomplete`，不得交付；全部完成後才建立 `Completed`。
- 每次重新執行建立獨立輸出目錄，不覆寫前次產出。

## 8. 跳點執行與最終交付

第一版由升級人員手動操作正式適配後的 Aras 升級工具；協調工具不直接啟動升級工具、不自動登入驗證、不自動備份或還原 DB。

每個跳點完成後由升級人員人工驗證：

- DB 中的 Innovator 版本為目標版本。
- 必要服務可啟動。
- 基本登入及 DB 連線檢查通過。
- 驗證時間、環境、實際版本、結果及畫面或等效 Log 已保存。
- 成功後由升級人員建立該版本 DB 備份點，才能解鎖下一跳點。

最終交付至少包含：

- R38 DB 備份檔及還原識別資訊。
- 各升級跳點正式適配 Package／客戶專用升級工具封存版本。
- Core Tree 比較產出目錄，且為 `Completed`、無待人工確認。
- 不可覆寫的執行歷程、執行嘗試、確認與更正紀錄。
- 差異摘要及驗證摘要可由工具自動產生。

## 9. Skill 分層架構

本次已確認採用「一個主 Skill＋功能 Skill＋細項執行單元」：

### 9.1 主 Skill

沿用並調整現有 `.agents/skills/aras-innovator-upgrade/`，作為總協調入口，負責：

- 判斷案件階段。
- 讀取案件、規格、術語及不可覆寫歷程。
- 路由至適用功能 Skill。
- 管理安全關卡、停止條件、證據、Rollback 及交接。

### 9.2 第一版功能 Skill 候選

- `aras-manage-upgrade-case`
- `aras-build-customer-package`
- `aras-prepare-ootb-hop-diff`
- `aras-prepare-adapted-package`
- `aras-manage-upgrade-rules`
- `aras-compare-core-tree`
- `aras-coordinate-upgrade-hop`
- `aras-assemble-upgrade-delivery`

### 9.3 細項執行單元

建立執行快照、追加歷程、取得目錄鎖、解析 AML、建立 CompareKey、Rule 1／Rule 2 單一步驟、二進位比較及完成標記等細節，原則上放在功能 Skill 的固定程序、`scripts/`、正式程式 command/action 及測試中。

只有具備獨立觸發需求、完整輸入輸出及獨立安全邊界的細項，才升格為頂層 Skill，避免產生大量重疊 Skill。

### 9.4 建立時程

1. 階段 0：建立 Skill Map，定義名稱、觸發條件、輸入、輸出、安全責任及相依關係。
2. 階段 1：更新主 Skill，建立 `aras-manage-upgrade-case`，同步完成案件與受控執行核心。
3. Package／AML 階段：建立客戶 Package、Rule 1、Rule 2 及規則管理功能 Skill。
4. Core Tree 階段：建立 Core Tree 功能 Skill 與細項執行程序。
5. 跳點／交付階段：建立跳點協調及最終交付 Skill。
6. 整合驗收階段：測試主 Skill 路由、各功能 Skill 的獨立情境、錯誤隔離與阻擋行為。

## 10. 第一階段實作邊界

第一階段先建立專案基礎、案件模型與受控執行核心：

- 案件、任務、升級路徑與跳點模型。
- 執行嘗試、執行快照、不可覆寫歷程與更正紀錄。
- 狀態轉換、單人確認關卡、一次性鎖定與工作目錄鎖。
- 安全白名單判定及中斷／安全重試資格。
- Skill Map、主 Skill 路由與第一個案件管理功能 Skill。
- 以介面或測試替身隔離 SQL、正式 DB、Aras Export、正式升級工具及 K: 目錄。

第一階段不直接連接客戶正式 DB、不修改正式 Package、不修改 R38 Core Tree，也不執行正式升級。

## 11. 主要依據文件

- `AGENTS.md`
- `CONTEXT.md`
- `.scratch/aras-upgrade-orchestrator/spec.md`
- `docs/standards/AML_Structure_and_Traversal_Standard.md`
- `docs/requirements/01_CrossVersion_Package_Comparison_Adjustment_Requirements.md`
- `docs/specs/11_CrossVersion_Package_Comparison_Adjustment_Spec.md`
- `.agents/skills/aras-innovator-upgrade/references/project-facts.md`
- `.agents/skills/aras-innovator-upgrade/references/terminology.md`
- `.agents/skills/aras-innovator-upgrade/references/upgrade-checkpoints.md`

## 12. 文件狀態

- 本摘要建立日期：2026-08-09。
- 需求訪談：主要需求已確認。
- 程式實作：由 `Aras 升級協調工具－第一階段實作` 對話接續。
- 第一版仍須依專案規格完成技術架構、資料模型、測試策略及驗收實作。
- 本文件未執行 `dotnet build` 或 `dotnet test`；本次僅建立文件。

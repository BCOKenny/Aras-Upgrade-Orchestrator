# Aras 升級協調工具－第一階段實作對話摘要

- 整理日期：2026-08-09
- 專案：Aras Upgrade Orchestrator
- 對話範圍：從實作階段啟動、Skill 架構納入第一階段、3.5 與 4A～4E 實作，到合併與 Git 收尾
- 文件定位：本文件是對話歷程備份，記錄當時的決策與完成狀態；專案後續若有演進，應以目前規格、Issue、Design 文件與程式碼為準

## 1. 實作啟動要求

需求訪談已完成，因此實作開始後不重新詢問已確認事項。工作開始前要求完整讀取並遵守下列文件：

- `AGENTS.md`
- `CONTEXT.md`
- `docs/agents/domain.md`
- `docs/agents/issue-tracker.md`
- `docs/standards/AML_Structure_and_Traversal_Standard.md`
- `.scratch/aras-upgrade-orchestrator/spec.md`
- `.agents/skills/aras-innovator-upgrade/SKILL.md` 及相關 references

實作前應先盤點：

- 現有專案內容
- 規格與程式的一致性
- Git 狀態及使用者既有變更
- 命名與責任邊界是否符合專案文件

只有會重大改變技術架構、且無法從既有專案文件推導的事項，才需要再次詢問。其餘可由現有規格決定者直接執行。

## 2. 第一階段目標與安全邊界

第一階段聚焦於「案件與受控執行核心」，主要能力包括：

- 升級案件
- 任務與任務相依圖
- 版本化升級跳點
- 每次獨立執行嘗試
- 不可覆寫、只能追加的執行歷程
- 更正紀錄保留原事件
- 狀態關卡與安全重試資格
- 目錄鎖
- 外部動作安全白名單
- 外部執行前的確認、快照與結果保存

安全限制如下：

- 不直接連接客戶正式 DB。
- 不直接修改正式 Package。
- 不操作 Core Tree。
- 不操作 `K:` 升級目錄，除非後續取得明確授權。
- 危險或外部操作先以明確介面、預設拒絕實作及測試替身隔離。
- AI 不得自行取代具名人工核准。
- 執行歷程、完成紀錄與已發布規則版本不得被覆寫。

## 3. Skill 三層架構決策

需求方再次確認：Skill 架構必須隨正式程式同步實作，不能等所有程式完成後再補做。

採用的架構為：

```text
一個主 Skill
    └─ 約八個可獨立驗收的功能 Skill
         └─ 固定程序、scripts、正式 command/action 或測試
```

### 3.1 主 Skill

沿用並調整 `.agents/skills/aras-innovator-upgrade/`，作為總協調入口，負責：

- 判斷案件目前階段
- 讀取案件、任務與不可覆寫執行歷程
- 選擇並路由至正確的功能 Skill
- 管理安全關卡、停止條件、證據與交接
- 在功能尚未實作或前置證據不足時明確停止

主 Skill 不複製 AML、Package、Core Tree 等細節，也不承載另一套核心業務邏輯。

### 3.2 功能 Skill

第一版規劃的功能責任如下：

| 功能 Skill | 責任邊界 |
|---|---|
| `aras-manage-upgrade-case` | 案件、路徑、任務、執行歷程、重試、目錄鎖與安全判定 |
| `aras-build-customer-package` | 一次性客戶 Package 基準流程與完成鎖定 |
| `aras-prepare-ootb-hop-diff` | Rule 1 OOTB 跳點 SourceDiff／TargetDiff |
| `aras-prepare-adapted-package` | Rule 2 正式適配 Package |
| `aras-manage-upgrade-rules` | Rule 1／Rule 2 規則驗證、發布、版本與例外 |
| `aras-compare-core-tree` | Core Tree 比較協調 |
| `aras-coordinate-upgrade-hop` | 單一升級跳點協調 |
| `aras-assemble-upgrade-delivery` | 最終升級交付組裝 |

名稱可依專案既有用語微調，但責任不得重疊。

### 3.3 細項執行能力

細項能力原則上留在功能 Skill 的固定程序、scripts、正式程式 command/action 或測試中。只有同時具備下列條件者，才可升格為獨立頂層 Skill：

- 可被獨立要求
- 有完整輸入與輸出契約
- 有獨立安全邊界
- 可獨立驗收

不為每個微小程式步驟建立 Skill。Skill 應呼叫或引用正式受測程式能力，不複製業務邏輯。

## 4. 對話中確認的實作順序

規劃與實際推進順序如下：

1. 階段 0：建立 Skill Map，定義名稱、觸發條件、輸入、輸出、安全責任與相依關係。
2. 階段 1：建立專案基礎、案件與受控執行核心，同步更新主 Skill 並建立 `aras-manage-upgrade-case`。
3. 階段 3.5：鎖定一次性客戶 Package 流程。
4. 階段 4A：建立 AML 核心。
5. 階段 4B：建立規則管理與版本化。
6. 階段 4C：建立 Rule 1 OOTB 跳點差異包。
7. 階段 4D：建立 Rule 2 正式適配 Package。
8. 階段 4E：執行 Package 整合測試。
9. 後續階段：Core Tree、跳點協調與最終交付，並同步建立相應功能 Skill 與低自由度執行單元。

因此，先前所說的「一次性 Package 流程鎖定及 AML／Rule 1／Rule 2 核心」位於整體實作順序的 3.5～4E 範圍。

## 5. 階段 3.5：一次性 Package 流程鎖定

此階段建立客戶 Package 基準的一次性流程與不可逆狀態關卡，重點包括：

- 在首次 DB 變更前固定 action、版本與 Checksum。
- 同一案件的一次性流程不得任意重開。
- DB 備份還原證據必須與固定資訊相符。
- Aras Export 排除項目未處置完成時，不得完成 Package 基準。
- 客戶 Package 基準完成後永久鎖定。
- 外部動作必須通過流程鎖與固定 Checksum 驗證。
- 預設外部執行器不得執行正式操作。

同時建立或更新 `aras-build-customer-package`，使 Skill 僅協調正式受測核心，不自行操作 DB 或 Aras Export。

## 6. 階段 4A：AML 核心

AML 實作遵守 `AML_Structure_and_Traversal_Standard.md`，不得把 AML 當成一般 XML。核心要求包括：

- 區分 AML Root、Item、Scalar Property、Item Property、Relationships Container 與 Relationship Item。
- 遞迴走訪所有巢狀層級，不假設固定深度。
- 安全解析並拒絕 DTD。
- 保留宣告的 Namespace 與 CDATA。
- Package XML 只依根目錄相對路徑配對。
- CompareKey 使用 `id` 或 canonicalized `where`。
- CompareKey 缺失或同側重複時轉人工確認。
- AML 語意比較可忽略純格式 Attribute 與 Relationship 順序。
- 重複 Scalar Property 等不可靠情況必須轉人工確認，不可自動猜測。

此階段提供後續 Rule 1、Rule 2 與 Package 比較的共用基礎。

## 7. 階段 4B：規則管理與版本化

此階段建立 Rule 1／Rule 2 共用規則治理能力，主要內容包括：

- 預設 Rule 2 七步規則集。
- 規則草稿結構與順序驗證。
- 拒絕重複順序及不支援步驟。
- 具名人工建立與發布權限。
- AI 不得自行建立或發布正式規則版本。
- 已發布版本只能追加，不可覆寫。
- 以 Checksum 固定不可變規則版本。
- 依 StepId 套用版本例外。
- 多個例外對同一步驟產生衝突結果時阻擋執行。
- 保存 Rule 1／Rule 2 實際使用的規則解析快照。

同步建立 `aras-manage-upgrade-rules`，其責任限於規則驗證、發布、版本與例外，不執行 AML 修改或 Package 作業。

## 8. 階段 4C：Rule 1 OOTB 跳點差異包

Rule 1 由兩個不可變 OOTB Solutions 建立跳點差異，核心輸出為：

- `SourceDiff`
- `TargetDiff`
- 處理摘要
- 完成或 Incomplete 狀態
- 單一 ZIP Checksum
- 可重用驗證資訊

關鍵行為包括：

- 依 Item 差異結果產生雙端差異內容。
- 同側重複 CompareKey 或不可靠 Scalar 配對時保留差異並轉人工確認。
- 單一 XML 錯誤可隔離，但整體完成狀態仍被阻擋。
- 未解除人工確認時只保存 Incomplete 摘要，不產生正式封裝。
- 差異包缺少任一端內容、處理摘要，或包含路徑逸出項目時不得重用。
- 原始 OOTB Package 必須維持不變。

同步建立 `aras-prepare-ootb-hop-diff`，並要求其引用正式核心及守住不可變輸入邊界。

## 9. 階段 4D：Rule 2 正式適配 Package

Rule 2 使用已驗證的 `TargetDiff`、不可變客戶 Package 基準及已解析的規則快照，建立正式適配 Package。主要內容包括：

- 先驗證 Rule 1 差異包與跳點一致性。
- 修改前先完成原始 Solutions 備份。
- 建立雙端 XML 工作副本，不直接修改原始輸入。
- 依七步規則處理直接 Scalar Property。
- 遞迴處理 Relationship 內的 Item 與 Property。
- 支援 federated Property 轉換。
- 不把 Item Attribute 當成 Scalar Property 修改。
- 遇到重複 Scalar 等不可靠狀態時可局部收集結果，但整體必須阻擋完成並轉人工確認。
- 備份失敗時不得寫入任何工作副本。

同步建立 `aras-prepare-adapted-package`，其責任不包含 DB、Core Tree 或未授權 `K:` 目錄操作。

## 10. 階段 4E：Package 整合測試

使用者要求執行 4E，並明確指示「如有問題，先中止」。整合測試驗證 3.5、4A、4B、4C 與 4D 的串接，重點情境包括：

- 從一次性客戶 Package 基準串接 Rule 1 與 Rule 2，完成正式適配 Package。
- 拒絕把已驗證差異包套用到不同升級跳點。
- Rule 1 封裝遭竄改時保持 Solutions 零寫入。
- Rule 2 尚有未解除人工確認時阻擋正式完成。
- 任一安全前置條件不成立時，不繼續危險或外部操作。

最終驗證結果：

- 測試：`58/58 tests passed`
- Release 建置：成功
- 警告：`0`
- 錯誤：`0`

建置過程曾在受限執行身分下遇到 OneDrive reparse-point 快取檔存取拒絕。調查確認檔案 ACL 正常、非唯讀，且問題不在原始碼；改以一般本機權限執行相同 Release 建置後成功，因此未修改專案程式來迴避環境問題。

## 11. Git 建立、提交、合併與收尾

對話中曾要求建立 Git 儲存庫、設定 `main`、加入 GitHub remote 並推送。後續 4B～4E 工作在功能分支完成並推送：

- Remote：`https://github.com/BCOKenny/Aras-Upgrade-Orchestrator.git`
- 功能分支：`codex/phase-4-package-rules`
- 初始提交：`b850120 first commit`
- 4A 提交：`7d3be9a Implement phase 4A AML foundation`
- 4B～4E 功能提交：`7158562 Implement package rule pipeline`
- Pull Request 合併提交：`4741922 Merge pull request #1 from BCOKenny/codex/phase-4-package-rules`

Git 收尾前先確認：

- `codex/phase-4-package-rules` 是 `origin/main` 的祖先，代表功能已合併。
- 工作目錄與索引乾淨。
- `main` 僅落後 `origin/main`，可安全 fast-forward。

收尾結果：

- 切回 `main`。
- 以 `git merge --ff-only origin/main` 同步至 `4741922`。
- 在同步後的 `main` 再次通過 58/58 測試與 Release 建置。
- 使用安全刪除移除本機 `codex/phase-4-package-rules`。
- 成功刪除遠端同名分支。
- 當時最終只保留 `main` 與 `origin/main`，兩者指向相同合併提交。

全程未使用 force push、hard reset 或強制分支刪除。

## 12. Codex 桌面介面操作插曲

對話中也處理了 Codex 桌面介面的終端機面板顯示方式：

- 終端機面板開啟後曾短暫顯示在下方又縮回。
- 使用者發現工具列的「開啟方式」可使終端機正常顯示於右側。
- 此項屬於 Codex 桌面介面配置，不影響專案程式、Package 流程或 Git 狀態。

## 13. 本對話結束時的完成範圍

本對話結束時已完成並驗證：

- 第一階段案件與受控執行核心
- Skill Map 與主 Skill 路由基礎
- `aras-manage-upgrade-case`
- 3.5 一次性客戶 Package 流程鎖定
- `aras-build-customer-package`
- 4A AML 核心
- 4B 規則管理與版本化
- `aras-manage-upgrade-rules`
- 4C Rule 1 OOTB 跳點差異包
- `aras-prepare-ootb-hop-diff`
- 4D Rule 2 正式適配 Package
- `aras-prepare-adapted-package`
- 4E Package 整合測試
- Pull Request 合併與 Git 分支收尾

## 14. 本對話結束時尚未涵蓋的後續能力

依當時規劃，後續仍需依序處理：

- Core Tree 比較能力及對應功能 Skill
- 單一升級跳點協調與狀態關卡
- 多跳點升級流程串接
- 最終升級交付組裝
- 主 Skill 對所有功能 Skill 的完整路由整合驗收
- 各功能 Skill 的獨立成功、錯誤、阻擋與停止情境驗收
- 在取得明確授權及完整證據前，不執行正式 DB、正式 Package、Core Tree 或 `K:` 目錄操作

上述是本對話當時的未完成清單；不代表 2026-08-09 專案目前仍未完成，確認現況時應重新檢查最新 Issue、Design 文件、測試與程式碼。

## 15. 持續適用的核心原則

- Skill 只負責協調、契約、安全關卡與證據，不複製正式核心邏輯。
- AML 必須依 Aras 結構語意處理，不能當成通用 XML。
- 所有輸入、規則版本、Checksum、執行嘗試與完成證據必須可追溯。
- 歷程與已發布版本只能追加，不得覆寫。
- 人工確認未完成、Checksum 不符、跳點不符、備份失敗或輸入遭竄改時必須停止。
- 外部與危險操作採預設拒絕，只有在明確授權與安全前置條件全部成立後才能執行。
- 不覆寫使用者既有變更，不以 Git 清理命令掩蓋不相關修改。

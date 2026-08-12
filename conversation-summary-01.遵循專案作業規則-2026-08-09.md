# 專案對話摘要 01：遵循專案作業規則

- 摘要日期：2026-08-09
- 專案：Aras Upgrade Orchestrator
- 文件性質：對話與作業限制摘要
- 影響範圍：僅新增本 Markdown 文件，不變更程式碼、設定、建置流程或專案執行行為

## 一、對話目的

本次對話先確認專案的 Git worktree 狀態，之後要求整理目前對話並在專案根目錄建立摘要文件。所有操作均應遵循專案作業規則，且不得影響專案執行。

## 二、原始作業限制

使用者最初指定以下限制：

1. 不建立 Git repository。
2. 不建立 branch。
3. 不建立 worktree。
4. 不執行 `git commit`。
5. 不執行 `dotnet test`。
6. `dotnet build` 最多執行一次。
7. 不進行廣泛重構。
8. 不修改與本次任務無關的功能。
9. 任務不清楚或範圍過大時，先提出簡短的分階段計畫並停止。
10. 僅完成指定任務，完成後停止。

## 三、worktree 詢問與確認結果

### worktree 說明

Git worktree 是與 Git repository 關聯的工作目錄，包含可編輯的專案檔案與目前檢出的 branch 或 commit。同一個 repository 可以登記主要工作目錄及額外的 linked worktree；額外 worktree 通常位於其他資料夾。

### 當時的唯讀檢查結果

- 專案路徑：`C:\Users\kenny\OneDrive\文件\Aras Upgrade Orchestrator`
- 此路徑為 Git 工作目錄。
- 當時登記的 worktree 數量為 1。
- 當時 branch：`main`
- 當時 HEAD：`b850120745ee093bf4891ecf52d30f05d1ecccee`
- 當時未發現額外的 linked worktree。

上述資訊是對話當時的檢查快照，不代表未來狀態不會改變。

## 四、目前有效的專案編輯規則

後續提供的 `AGENTS.md` 指示已取代先前的 `AGENTS.md` 指示。凡在本專案建立、修改、搬移或刪除檔案，必須先讀取並遵守：

` .agents/skills/edit-project-directly/SKILL.md `

本次建立摘要時實際採用的重點如下：

- 僅能修改目前專案目錄內的檔案。
- 不建立 Git repository。
- 不建立或切換 branch。
- 不建立或使用 Git worktree。
- 未經本次任務明確要求，不執行 `dotnet test`。
- 只有在驗證程式碼變更確有必要時才可執行 `dotnet build`，且每次使用者要求最多一次。
- 僅修改與當前要求直接相關的檔案，不進行專案範圍重構。
- 新增或更新的專案文件預設使用繁體中文（zh-TW），技術識別字與產品名稱維持原文。
- 完成前以非 Git 方式檢查本次觸及的檔案範圍。

## 五、其他專案規範索引

- Issue tracker：`.scratch/` 下的 Markdown 文件；規則見 `docs/agents/issue-tracker.md`。
- Domain docs：採單一 bounded context 文件配置；規則見 `docs/agents/domain.md`。
- 涉及 Aras AML 解析、比較、複製、修改、輸出、Package upgrade 或相關測試前，必須先讀取 `docs/standards/AML_Structure_and_Traversal_Standard.md`。
- AML 不得視為一般 XML；必須區分 AML Root、Item、Scalar Property、Item Property、Relationships Container 與 Relationship Item，並以不假設固定深度的方式遞迴處理。

本次工作只建立對話摘要，不涉及 AML 或 Issue tracker 操作，因此未啟動相關流程。

## 六、本次文件建立範圍

本次僅新增：

`conversation-summary-01.遵循專案作業規則-2026-08-09.md`

未修改任何既有檔案，未建立 repository、branch 或 worktree，未執行 `git commit`、`dotnet build` 或 `dotnet test`，亦未影響專案執行。

# 專案對話摘要 01

- 整理日期：2026-08-09
- 專案：Aras Upgrade Orchestrator
- 文件用途：備份目前專案對話與已確認的作業規則
- 影響範圍：僅新增本文件，不變更程式碼、設定或執行流程

## 1. 對話重點

本次對話先確認專案的 Git worktree 狀態，接著要求將目前專案對話整理成 Markdown 文件，並存放於專案內。文件建立應遵循專案規則，且不得影響專案執行。

## 2. Git worktree 確認

Git worktree 是與 repository 關聯的工作目錄，可包含目前檢出的 branch 與 commit；同一個 repository 也可以有額外的 linked worktree。

當時唯讀檢查結果：

- 專案路徑：`C:\Users\kenny\OneDrive\文件\Aras Upgrade Orchestrator`
- 目前專案是 Git 工作目錄。
- 登記的 worktree 共 1 個，只有專案本身的主要工作目錄。
- Branch：`main`
- HEAD：`b850120745ee093bf4891ecf52d30f05d1ecccee`
- 未發現額外的 linked worktree。

以上為檢查當時的狀態快照。

## 3. 作業規則摘要

使用者指定的限制包括：

1. 不建立 Git repository、branch 或 worktree。
2. 不執行 `git commit`。
3. 不執行 `dotnet test`。
4. `dotnet build` 每次任務最多一次。
5. 不進行廣泛重構，也不修改與任務無關的功能。
6. 任務不清楚或過於廣泛時，先提出分階段計畫並停止。
7. 只完成指定任務，完成後停止。

## 4. 目前專案編輯規範

後續提供的 `AGENTS.md` 指示取代先前版本。凡建立、修改、搬移或刪除專案檔案，必須先遵守：

`.agents/skills/edit-project-directly/SKILL.md`

其重點為：

- 僅修改目前專案目錄內的檔案。
- 專案文件預設使用繁體中文（zh-TW），技術識別字、路徑、命令與產品名稱保留原文。
- 只修改與當前要求直接相關的檔案。
- 完成前以非 Git 方式檢查觸及檔案範圍，並回報是否執行 build 或 test。

其他規範索引：

- Issue tracker：`.scratch/`，詳見 `docs/agents/issue-tracker.md`。
- Domain docs：詳見 `docs/agents/domain.md`。
- 涉及 Aras AML 前，必須先讀取 `docs/standards/AML_Structure_and_Traversal_Standard.md`，並依該標準區分 AML Root、Item、Scalar Property、Item Property、Relationships Container 與 Relationship Item。

## 5. 本次產出

本次僅新增：

`docs/backup/conversation-summary-01.遵循專案作業規則.md`

未執行 `dotnet build` 或 `dotnet test`，未執行 Git 寫入操作，未建立或使用 worktree，未影響專案執行。

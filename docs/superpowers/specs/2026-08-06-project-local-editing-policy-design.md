# Project-local editing policy Skill 設計

## 目標

確保本專案每個會變更檔案的工作，自動遵守本機直接編輯限制與文件語言政策，不需要使用者在每次任務重複指定。

## 架構

`AGENTS.md` 是強制專案入口。Agent 在建立、修改、搬移或刪除專案檔案前，必須載入 `.agents/skills/edit-project-directly/SKILL.md`。該 Skill 是詳細政策的 single source of truth；`AGENTS.md` 只保存觸發條件與優先順序規則。

## Skill 內容

建立自包含 Skill：

- `.agents/skills/edit-project-directly/SKILL.md`
- `.agents/skills/edit-project-directly/agents/openai.yaml`

不需要 scripts、references、assets 或輔助 README。

Skill 必須要求 Agent：

1. 只在目前專案目錄內修改檔案。
2. 不初始化或建立 Git repository。
3. 不建立或切換 branch。
4. 不建立或使用 Git worktree。
5. 除非使用者在目前任務明確要求，否則不執行 `dotnet test`。
6. 除非驗證目前需求的程式碼變更確有必要，否則不執行 `dotnet build`。執行前必須先說明必要原因，且每個使用者需求最多執行一次。
7. 只修改目前需求直接相關的檔案，避免全專案重構。
8. 預設以繁體中文（zh-TW）建立及更新 Specification、FunctionSpec、WorkSpec、Design document、Report、Guide、README content、User-facing project explanation，以及其他專案文件；使用者明確要求其他語言時除外。
9. 保留 technical identifier、檔名、路徑、code symbol、command name、API name、XML／AML element、property name、Aras Innovator 與第三方產品或框架正式名稱的原始語言；不得只為符合文件語言政策而翻譯原始碼。
10. 本專案規則優先於其他 Skill 的衝突工作流程。若工作流程要求受禁止的動作，跳過該動作並回報衝突，不得改用其他 Git 操作替代。
11. 完成前，在不使用 Git 的情況下檢查實際修改範圍，並回報是否執行 build 或 test。

## 觸發條件與優先順序

`AGENTS.md` 指示適用於本專案所有可能變更檔案系統狀態的工作，包括原始碼、文件、設定、產生檔及 Skill 檔案。唯讀檢查與說明不需要載入此 Skill。

使用者直接指示可以進一步縮小工作範圍。只有目前需求中的明確指示可以授權 `dotnet test` 或覆寫文件預設語言；其他 Git repository、branch、worktree、build 次數及最小修改限制持續有效，直到使用者更新 `AGENTS.md` 或此 Skill。

## 驗證

不使用 Git 且不執行 `dotnet test`：

- 對 Skill 目錄執行 Skill Creator 結構驗證器。
- 寫入後重新讀取 `AGENTS.md`、`SKILL.md` 與 `agents/openai.yaml`。
- 確認 Skill 名稱、YAML frontmatter、UI metadata、觸發文字、引用路徑及 zh-TW 政策一致。
- 本變更只涉及文件，不執行 `dotnet build`。

## 修改檔案

實作限制於：

- `AGENTS.md`
- `docs/superpowers/specs/2026-08-06-project-local-editing-policy-design.md`
- `.agents/skills/edit-project-directly/SKILL.md`
- `.agents/skills/edit-project-directly/agents/openai.yaml`

保留本文件作為核准的設計紀錄，不建立 Git commit。

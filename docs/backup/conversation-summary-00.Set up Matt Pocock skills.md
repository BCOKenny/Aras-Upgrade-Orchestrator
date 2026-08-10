# 專案對話備份摘要

- 備份日期：2026-08-09
- 專案：Aras Upgrade Orchestrator
- 用途：僅保存本次 Codex 對話與設定決策，不是執行期設定，不供程式載入。

## 對話摘要

### 1. 設定 Matt Pocock 工程 Skill

使用 `/setup-matt-pocock-skills` 啟動專案設定流程。

初始盤點結果：

- 當時專案目錄尚無 Git remote、`AGENTS.md`、`CLAUDE.md`、`CONTEXT.md`、`CONTEXT-MAP.md` 或 `docs/agents/`。
- 未安裝 `triage` Skill，因此未建立 triage label 設定。
- 未發現 monorepo 訊號，因此採用 single-context 領域文件布局。

使用者選擇：

- Issue tracker：Local Markdown
- Agent 指示檔：`AGENTS.md`

已建立的專案設定檔：

- `AGENTS.md`
- `docs/agents/issue-tracker.md`
- `docs/agents/domain.md`

設定內容重點：

- Issue 與 spec 放在 `.scratch/`。
- 每個 feature 使用獨立目錄。
- 單一 Context 使用根目錄 `CONTEXT.md` 與 `docs/adr/`。
- 缺少 `CONTEXT.md` 或 ADR 時，工程 Skill 靜默繼續，不要求預先建立。

### 2. 對話語言

使用者要求後續使用繁體中文對話，已遵循。

### 3. 全域 Skill 盤點

確認本機全域 Skill 位於：

`C:\Users\kenny\.codex\skills`

盤點到的工程相關 Skill 包含：

- `setup-matt-pocock-skills`
- `to-spec`
- `to-tickets`
- `implement`
- `tdd`
- `diagnosing-bugs`
- `code-review`
- `domain-modeling`
- `grilling`
- `grill-with-docs`
- `writing-great-skills`

系統 Skill 包含：

- `imagegen`
- `openai-docs`
- `skill-creator`
- `skill-installer`
- `plugin-creator`
- `review-agent`

實際每輪是否套用 Skill，取決於使用者要求是否符合該 Skill 的觸發條件，也可以在要求中直接指定 Skill 名稱。

### 4. `writing-great-skills` 與 `skill-creator` 的關係

結論：兩者互補，通常不衝突。

- `skill-creator`：主要執行流程，負責建立目錄、撰寫、驗證及測試 Skill。
- `writing-great-skills`：寫作與設計準則，強調可預測性、精簡、觸發條件、漸進揭露、單一真實來源及避免重複。

建議使用順序：

1. 以 `skill-creator` 規劃並執行建立或修改流程。
2. 以 `writing-great-skills` 審查內容品質與可預測性。
3. 使用 `skill-creator` 的驗證工具完成檢查。

已指出一項格式差異：`skill-creator` 的內建說明要求 YAML frontmatter 僅含 `name` 與 `description`；`writing-great-skills` 則討論 `disable-model-invocation: true`。建立新 Skill 時，優先依目前系統內建 `skill-creator` 規範，除非另有明確需求且確認執行環境支援該欄位。

### 5. Skill YAML frontmatter

確認 `SKILL.md` 的 YAML frontmatter 必須位於 Markdown 標題之前，並以三個 `---` 包住：

```markdown
---
name: example-skill
description: 說明 Skill 用途及觸發情境。
---

# Example Skill
```

系統內建 `skill-creator` 要求：

- frontmatter 位於檔案最前方。
- 必須包含 `name`。
- 必須包含 `description`。
- Skill 名稱使用小寫英文字母、數字及連字號。
- Markdown 標題放在 YAML 結束後。

## 本備份的檔案與執行影響

本檔案位於 `docs/backup/`，用途僅為對話備份。它不會被加入 `.csproj`、執行腳本、CLI 路由或任何 runtime 設定，因此不影響專案建置、測試與執行。

本次未執行 `dotnet build` 或 `dotnet test`；因為本次只新增文件備份，沒有必要進行程式驗證。

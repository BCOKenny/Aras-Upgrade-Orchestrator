# Core Tree Comparison Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建立可驗證三份輸入、比較 Client／Server 並輸出 A／B／C 與完成狀態的離線 Core Tree 正式核心及功能 Skill。

**Architecture:** 將輸入驗證、內容比較、邏輯路徑配對、分類決策及檔案產出拆成獨立核心單元。比較引擎保持純決策，builder 集中處理新嘗試目錄、租約、複製與標記。

**Tech Stack:** .NET 8、System.IO、System.Text.Json、現有零外部套件測試執行器、Agent Skill Markdown/YAML。

## Global Constraints

- 不連接 DB、Aras 工具、正式 Core Tree 或 K:。
- 不合併或修改 R38 Core Tree。
- 只在相同相對目錄及相同主檔名套用固定副檔名演進。
- 新執行嘗試不得覆寫；輸入與輸出不得重疊。
- 多候選保持人工確認，不能產生 `Completed`。

---

### Task 1: 輸入證據與內容比較

**Files:**
- Create: `src/ArasUpgradeOrchestrator.Core/CoreTrees/CoreTreeModels.cs`
- Create: `src/ArasUpgradeOrchestrator.Core/CoreTrees/CoreTreeInputValidator.cs`
- Create: `src/ArasUpgradeOrchestrator.Core/CoreTrees/CoreTreeContentComparer.cs`
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**
- Produces: `CoreTreeComparisonRequest`, `CoreTreeInputValidator.Validate`, `CoreTreeContentComparer.AreEqualAsync`。

- [ ] 寫入版本證據、Client 文字、Server 規則與二進位比較的失敗測試。
- [ ] 執行測試並確認因 CoreTrees 型別不存在而失敗。
- [ ] 實作最小模型、驗證器及比較器。
- [ ] 執行測試並確認通過。

### Task 2: 邏輯配對與 A／B／C 決策

**Files:**
- Create: `src/ArasUpgradeOrchestrator.Core/CoreTrees/CoreTreeLogicalPathResolver.cs`
- Create: `src/ArasUpgradeOrchestrator.Core/CoreTrees/CoreTreeComparisonEngine.cs`
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**
- Consumes: `CoreTreeComparisonRequest`, `CoreTreeContentComparer`。
- Produces: `CoreTreeComparisonResult`、分類項目及人工確認。

- [ ] 寫入 A／B／C、副檔名演進與多候選失敗測試。
- [ ] 執行測試並確認因 resolver／engine 不存在而失敗。
- [ ] 實作固定演進與分類決策。
- [ ] 執行測試並確認通過。

### Task 3: 新嘗試產出、狀態與目錄鎖

**Files:**
- Create: `src/ArasUpgradeOrchestrator.Core/CoreTrees/CoreTreeComparisonBuilder.cs`
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**
- Consumes: `CoreTreeComparisonEngine`、`DirectoryLeaseManager`。
- Produces: A／B／C 目錄、`processing-summary.json`、`manual-reviews.json`、`Incomplete` 或 `Completed`。

- [ ] 寫入輸出結構、不可覆寫、輸入不變及完成阻擋失敗測試。
- [ ] 執行測試並確認 builder 尚不存在。
- [ ] 實作安全複製、摘要及狀態標記。
- [ ] 執行測試並確認通過。

### Task 4: 功能 Skill 與專案追蹤

**Files:**
- Create: `.agents/skills/aras-compare-core-tree/SKILL.md`
- Create: `.agents/skills/aras-compare-core-tree/references/core-capabilities.md`
- Create: `.agents/skills/aras-compare-core-tree/agents/openai.yaml`
- Create: `.scratch/aras-upgrade-orchestrator/issues/12-core-tree-comparison.md`
- Modify: `docs/design/skill-map.md`
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**
- Skill 必須引用 `CoreTreeInputValidator`、`CoreTreeContentComparer`、`CoreTreeLogicalPathResolver`、`CoreTreeComparisonEngine`、`CoreTreeComparisonBuilder`。

- [ ] 先寫 Skill 契約失敗測試並確認失敗。
- [ ] 建立最小 Skill、能力對照與 metadata。
- [ ] 更新 issue 與 Skill Map 階段狀態。
- [ ] 執行完整 Release build 與測試。

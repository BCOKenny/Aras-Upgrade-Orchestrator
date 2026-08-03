# 4C Rule 1 OOTB Hop Difference Package Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建立不修改原始 OOTB Package、可隔離錯誤並可驗證重用的 Rule 1 `SourceDiff`／`TargetDiff` 可攜式差異包。

**Architecture:** `Rule1DiffEngine` 負責單一 AML 文件的 Item 分類；`OotbHopDiffBuilder` 負責目錄、工作輸出與逐檔隔離；`OotbHopDiffPackager` 與 verifier 負責完成標記、單一 ZIP Checksum 及重用驗證。所有 AML 操作重用 4A 公開核心，規則身分固定使用 4B 的已發布版本參考。

**Tech Stack:** .NET 8、System.Xml.Linq、System.IO.Compression、System.Text.Json、SHA-256、現有單一可執行測試專案。

## Global Constraints

- 原始來源與目標 OOTB `Solutions` 只讀且不可修改。
- 只處理 XML，僅依 Package 根目錄相對路徑配對；非 XML 不比較、不複製。
- AML 必須依共享標準無固定深度遞迴，並保留 XML declaration、Namespace、CDATA、attributes 與完整子樹。
- 只使用已發布且固定版本／Checksum 的 Rule 1 規則；不計算逐 XML Checksum。
- 人工確認或錯誤未解除時不得標記 `Completed` 或產生可重用封裝。
- 每次重跑建立新嘗試與新輸出，不覆寫既有結果。

---

### Task 1: Rule 1 AML 文件差異引擎

**Files:**
- Create: `src/ArasUpgradeOrchestrator.Core/Packages/Rule1DiffEngine.cs`
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**
- Consumes: `AmlDocument`, `PackageCompareKeyIndex`, `AmlSemanticComparer`。
- Produces: `Rule1DiffEngine.Compare(AmlDocument source, AmlDocument target)` → `Rule1DocumentDiff`，包含兩端輸出、統計與人工確認。

- [ ] 寫入測試，使用已知 AML 驗證來源單側刪除、目標單側保留、相同雙刪、差異雙留。
- [ ] 執行測試並確認因 `Rule1DiffEngine` 不存在而失敗。
- [ ] 實作最小 Item 配對與分類，從 clone 移除應刪除的 top-level Item，人工確認 Item 保持不變。
- [ ] 執行全部測試並確認新舊測試通過。
- [ ] 增加深層 Item Property／Relationships 測試，確認相同判斷重用 `AmlSemanticComparer` 而非淺層 XML 比較。

### Task 2: OOTB 目錄建立器與錯誤隔離

**Files:**
- Create: `src/ArasUpgradeOrchestrator.Core/Packages/OotbHopDiffBuilder.cs`
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**
- Consumes: `PackageXmlPathMatcher.Match`, `Rule1DiffEngine.Compare`, `PublishedRuleSet`。
- Produces: `OotbHopDiffBuilder.BuildAsync(OotbHopDiffRequest)` → `OotbHopDiffBuildResult`，以及全新的 `SourceDiff`／`TargetDiff` 目錄。

- [ ] 寫入測試，建立來源／目標 fixture 目錄並驗證相對路徑配對、非 XML 忽略與原始檔案內容不變。
- [ ] 執行測試並確認因 builder 不存在而失敗。
- [ ] 實作輸入／輸出不重疊、輸出目錄必須不存在、Rule 1 已發布版本與 Checksum 驗證。
- [ ] 實作逐檔處理與原 XML declaration／Namespace／空 AML 保存。
- [ ] 增加一個損壞 XML fixture，驗證該檔記錄錯誤、其他檔仍完成處理且整體保持阻擋。
- [ ] 執行全部測試。

### Task 3: 完成標記、封裝與重用驗證

**Files:**
- Create: `src/ArasUpgradeOrchestrator.Core/Packages/OotbHopDiffArtifact.cs`
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**
- Consumes: `OotbHopDiffBuildResult`。
- Produces: `OotbHopDiffPackager.PackageAsync(...)` 與 `OotbHopDiffArtifactVerifier.VerifyAsync(...)`。

- [ ] 寫入成功測試，驗證 summary、Completed manifest、ZIP 與單一 SHA-256 Checksum。
- [ ] 執行測試並確認 packager 不存在。
- [ ] 實作 deterministic entry ordering 的 ZIP 封裝、manifest 寫入及封裝 Checksum。
- [ ] 寫入阻擋測試，驗證人工確認或錯誤存在時只有 Incomplete summary，沒有可重用 ZIP。
- [ ] 實作 verifier，核對完成狀態、來源／目標版本、規則 ID／版本／Checksum 與 ZIP Checksum。
- [ ] 寫入封裝竄改及版本不符測試並執行全部測試。

### Task 4: 功能 Skill、文件與驗收

**Files:**
- Create: `.agents/skills/aras-prepare-ootb-hop-diff/SKILL.md`
- Create: `.agents/skills/aras-prepare-ootb-hop-diff/agents/openai.yaml`
- Create: `.agents/skills/aras-prepare-ootb-hop-diff/references/core-capabilities.md`
- Create: `.scratch/aras-upgrade-orchestrator/issues/09-rule1-ootb-hop-diff.md`
- Create: `docs/design/phase-4c-rule1-ootb-hop-diff.md`
- Modify: `docs/design/skill-map.md`
- Modify: `README.md`
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**
- Consumes: Task 1–3 的正式受測核心與 `aras-manage-upgrade-rules`。
- Produces: 可被主 Skill 路由的 `aras-prepare-ootb-hop-diff` 固定程序與核心能力對照。

- [ ] 先加入 Skill 結構及責任邊界測試，確認因 Skill 尚不存在而失敗。
- [ ] 使用 `skill-creator` 初始化功能 Skill，再寫入只引用正式核心的固定程序。
- [ ] 文件明確記錄不可變輸入、人工確認阻擋、單一封裝 Checksum、Rule 2 只使用工作副本及尚未提供 UI／CLI。
- [ ] 執行 Release 建置、全部測試、`git diff --check` 與 Skill 驗證；若快速驗證器缺少 PyYAML，以專案內等價結構測試記錄限制。

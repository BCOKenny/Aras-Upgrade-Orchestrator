# Core Tree Capability Skills Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 將 Core Tree 比較拆成五個可直接呼叫、可由父 Skill 協調、具有語言中立契約與驗收案例的細項能力 Skill，並以同一組案例驗證現有 C# 參考實作。

**Architecture:** 保留 `aras-compare-core-tree` 作為父 Skill；新增輸入驗證、內容比較、邏輯檔案配對、A／B／C 分類及交付建立五個頂層 Skill。每個 Skill 自有精簡 `SKILL.md`、輸入／輸出／規則／停止條件 references、機器可讀 JSON 驗收案例及 Skill 行為測試證據；共用術語與跨 Skill 契約集中於領域文件，現有 C# 類別僅是第一個符合契約的參考實作。

**Tech Stack:** Markdown、Agent Skills (`SKILL.md`、`agents/openai.yaml`)、JSON、.NET 8／C#、現有 console test harness、Git。

## Global Constraints

- 所有 Skill 內容以中文為主；只有能以一句英文完整表達的內容及必要專業術語使用英文。
- Skill 依穩定業務能力命名，不依 `.cs` 類別或函式命名。
- 每個細項 Skill 必須能直接呼叫，也必須能由 `aras-compare-core-tree` 組合。
- 每個 Skill 必須包含輸入、輸出、固定規則、錯誤／停止條件、代表性輸入及預期結果。
- Skill 是業務契約與驗收規格來源；C#、Python 或其他程式只是可替換實作。
- 新實作必須通過相同 JSON 驗收案例後才能登錄為符合契約的實作。
- 現有 C# 核心不得因本計畫刪除；第一階段只補契約、Skill、驗收資產及必要的符合性測試。
- 不合併或修改 R38 Core Tree；三份 Core Tree 輸入保持唯讀。
- Core Tree XML 使用既有文字／二進位內容規則，不使用 Package AML 語意相等；涉及 AML 時仍完整遵守 `docs/standards/AML_Structure_and_Traversal_Standard.md`。
- A：客戶存在、來源 OOTB 不存在；與目標版邏輯檔案碰撞時轉人工確認。
- B：客戶與來源 OOTB 不同，目標版沒有唯一邏輯對應。
- C：客戶與來源 OOTB 不同，目標版有唯一邏輯對應。
- 多候選不猜測、不建立分類 D；錯誤或人工確認未清除時不得建立 `Completed`。
- C 類來源複本使用目標版檔名／副檔名但不轉換內容；A、B 保留來源檔名。
- 每次重試建立新執行嘗試，不覆寫舊結果。
- 建立或修改每一個 Skill 時，必須逐一使用 `superpowers:writing-skills` 的 RED／GREEN／REFACTOR；不得一次建立五個後才整批測試。
- 每一個程式或行為變更遵守 `superpowers:test-driven-development`；先看到預期失敗，再建立最小產物使其通過。
- 不修改或提交 `.superpowers/` 視覺訪談暫存內容。

---

## File Map

### Domain and architecture

- Modify: `CONTEXT.md` — 新增「細項能力 Skill」「Skill 契約」「語言中立驗收案例」「符合契約的實作」正式術語。
- Modify: `docs/adr/0002-three-layer-skill-architecture.md` — 保留原決策，標示由 ADR 0003 補充。
- Create: `docs/adr/0003-language-neutral-capability-skills.md` — 記錄細項業務能力升格 Skill 的決策及取捨。
- Modify: `docs/design/skill-map.md` — 加入五個 Core Tree 細項 Skill、直接呼叫、相依、驗收及參考實作。
- Create: `docs/design/core-tree-capability-contract.md` — 定義共用結果 envelope、代碼分類、相對路徑、排序及符合性規則。
- Create: `.scratch/aras-upgrade-orchestrator/issues/13-core-tree-capability-skills.md` — 追蹤試點實作及驗收。

### Parent Skill

- Modify: `.agents/skills/aras-compare-core-tree/SKILL.md` — 改為按任務路由五個細項 Skill，不把 C# 類別當成唯一規格來源。
- Modify: `.agents/skills/aras-compare-core-tree/references/core-capabilities.md` — 將「業務契約 → Skill → 目前 C# 參考實作」三者分開列出。

### Child Skill packages

每個目錄建立下列固定檔案：

```text
.agents/skills/<skill-name>/
├─ SKILL.md
├─ agents/openai.yaml
├─ references/input-contract.md
├─ references/output-contract.md
├─ references/rules.md
├─ references/error-and-stop-conditions.md
├─ references/skill-test-evidence.md
└─ assets/acceptance-cases/<case-id>/
   ├─ input.json
   └─ expected/result.json
```

五個 `<skill-name>`：

- `aras-validate-core-tree-inputs`
- `aras-compare-core-tree-content`
- `aras-resolve-core-tree-file-mappings`
- `aras-classify-core-tree-differences`
- `aras-build-core-tree-delivery`

### Tests

- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs` — 登錄架構、Skill package、父 Skill 路由及符合性測試。
- Create: `tests/ArasUpgradeOrchestrator.Core.Tests/CoreTreeCapabilitySkillTests.cs` — 檢查五個 Skill package 結構、frontmatter、直接呼叫、引用及驗收案例完整性。
- Create: `tests/ArasUpgradeOrchestrator.Core.Tests/CoreTreeCapabilityFixtureTests.cs` — 讀取共同 JSON 案例並呼叫現有 C# 核心驗證結果。

---

### Task 1: Record the architecture decision and domain language

**Files:**
- Create: `.scratch/aras-upgrade-orchestrator/issues/13-core-tree-capability-skills.md`
- Modify: `CONTEXT.md`
- Modify: `docs/adr/0002-three-layer-skill-architecture.md`
- Create: `docs/adr/0003-language-neutral-capability-skills.md`
- Modify: `docs/design/skill-map.md`
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**
- Consumes: approved design `docs/superpowers/specs/2026-08-04-core-tree-capability-skills-design.md`.
- Produces: canonical domain terms and an accepted ADR that later Skill tasks must cite.

- [ ] **Step 1: Claim implementation issue 13**

Create the issue with `Type: task`, `Status: claimed`, a link to the approved design, the five Skill names, and acceptance bullets for direct invocation, language-neutral contracts, per-Skill RED／GREEN／REFACTOR evidence, parent routing, fixture conformance and no modification of Core Tree inputs.

- [ ] **Step 2: Add the failing architecture documentation test**

Register `("Core Tree 細項能力 Skill 架構具有 ADR 與領域術語", CoreTreeCapabilitySkillArchitectureIsRecorded)` in `Program.cs`, then add:

```csharp
static Task CoreTreeCapabilitySkillArchitectureIsRecorded()
{
    var context = File.ReadAllText(ProjectPath("CONTEXT.md"));
    foreach (var term in new[] { "細項能力 Skill", "Skill 契約", "語言中立驗收案例", "符合契約的實作" })
        Assert.True(context.Contains($"**{term}**:", StringComparison.Ordinal), $"CONTEXT.md 缺少 {term}。 ");

    var adr2 = File.ReadAllText(ProjectPath("docs", "adr", "0002-three-layer-skill-architecture.md"));
    var adr3 = File.ReadAllText(ProjectPath("docs", "adr", "0003-language-neutral-capability-skills.md"));
    Assert.True(adr2.Contains("ADR 0003", StringComparison.Ordinal));
    Assert.True(adr3.Contains("語言中立驗收案例", StringComparison.Ordinal));
    Assert.True(adr3.Contains("不依程式語言或類別切分", StringComparison.Ordinal));
    return Task.CompletedTask;
}
```

- [ ] **Step 3: Run the test and verify RED**

Run: `dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests -c Release`

Expected: FAIL because ADR 0003 and the four `CONTEXT.md` terms do not yet exist.

- [ ] **Step 4: Add the exact domain terms**

Append these definitions to `CONTEXT.md` using the existing glossary format:

- `細項能力 Skill`：可由同事獨立提出、具完整輸入輸出與安全邊界，亦可由父 Skill 組合的穩定業務能力。Avoid：C# 類別 Skill、微小函式 Skill。
- `Skill 契約`：Skill 對輸入、輸出、固定規則、錯誤、停止與驗收結果的語言中立承諾。Avoid：目前程式行為、只有操作說明的 Markdown。
- `語言中立驗收案例`：不依賴實作語言、包含固定輸入及預期結果，供所有符合契約實作共同通過的案例。Avoid：只適用單一測試框架的案例。
- `符合契約的實作`：已通過指定 Skill 全部語言中立驗收案例並保存版本及驗收證據的程式實作。Avoid：AI 臨時產生且未驗證的程式。

- [ ] **Step 5: Record ADR 0003 and amend ADR 0002 without erasing history**

In ADR 0002 change the status line to `狀態：已採用；由 ADR 0003 補充` and add one sentence stating that low-freedom technical units remain code, while independently requestable business capabilities may become Skills under ADR 0003.

Create ADR 0003 with status `accepted`, the chosen five-independent-Skills option, the rejected parent-reference and single-mode-dispatch options, and the consequence that any implementation must pass the same acceptance cases before formal use.

- [ ] **Step 6: Add the five planned rows to Skill Map**

Add five rows beneath `aras-compare-core-tree` with status `依 ADR 0003 建置中`. Use the exact inputs, outputs, safety responsibilities and dependencies from design sections 7.1–7.5; each row must cite its acceptance-case directory and current C# reference type without calling that type the specification.

- [ ] **Step 7: Run verification and confirm GREEN**

Run: `dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests -c Release`

Expected: all tests PASS.

- [ ] **Step 8: Commit the governance slice**

```powershell
git add -- CONTEXT.md docs/adr/0002-three-layer-skill-architecture.md docs/adr/0003-language-neutral-capability-skills.md docs/design/skill-map.md .scratch/aras-upgrade-orchestrator/issues/13-core-tree-capability-skills.md tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs
git commit -m "Define language-neutral capability Skill architecture"
```

---

### Task 2: Define shared Core Tree capability contract and test helpers

**Files:**
- Create: `docs/design/core-tree-capability-contract.md`
- Create: `tests/ArasUpgradeOrchestrator.Core.Tests/CoreTreeCapabilitySkillTests.cs`
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**
- Consumes: ADR 0003 and the five canonical Skill names.
- Produces: `CoreTreeCapabilitySkillTests.AssertPackage(string skillName, IReadOnlyList<string> caseIds)` and the common contract version `core-tree-capabilities/1`.

- [ ] **Step 1: Register the failing shared-contract test**

Add `("Core Tree 細項能力共用契約固定結果與狀態語彙", CoreTreeCapabilityContractIsStable)` to `Program.cs` and define it to require:

```csharp
static Task CoreTreeCapabilityContractIsStable()
{
    var contract = File.ReadAllText(ProjectPath("docs", "design", "core-tree-capability-contract.md"));
    foreach (var token in new[]
    {
        "core-tree-capabilities/1", "Equal", "Different", "None", "Unique", "Ambiguous",
        "TextDecodeFallback", "MultipleTargetMappings", "CustomerAdditionCollidesWithTarget",
        "InvalidRequest", "InputDirectoryMissing", "VersionEvidenceMismatch", "RequiredTreeStructureMissing",
        "InputDirectoryOverlap", "InputOutputOverlap", "InvalidServerRuleSet", "RuleChecksumMismatch",
        "OutputAttemptAlreadyExists", "FileReadError",
        "ReadyToComplete", "Blocked", "Incomplete", "Completed"
    })
        Assert.True(contract.Contains($"`{token}`", StringComparison.Ordinal), $"共用契約缺少 {token}。 ");
    return Task.CompletedTask;
}
```

- [ ] **Step 2: Run and verify RED**

Run: `dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests -c Release`

Expected: FAIL because `docs/design/core-tree-capability-contract.md` is absent.

- [ ] **Step 3: Create the common contract**

Define this result envelope exactly:

```json
{
  "contractVersion": "core-tree-capabilities/1",
  "capability": "<canonical-skill-name>",
  "status": "<capability-status>",
  "result": {},
  "messages": [
    {
      "kind": "Notice|ManualReview|Error",
      "code": "<stable-code>",
      "relativePath": "Client/example.js",
      "details": {}
    }
  ],
  "evidence": {
    "inputIds": [],
    "ruleVersion": "",
    "ruleChecksum": ""
  }
}
```

The document must define forward-slash relative paths, ordinal-ignore-case path sorting, semantic JSON comparison, byte-for-byte delivery file comparison, immutable inputs and the result/code taxonomy approved in design section 8. `None` is a mapping result, not an error; `Incomplete` is a delivery status, not an error code.

- [ ] **Step 4: Add the reusable Skill package assertion**

Create `CoreTreeCapabilitySkillTests.cs` with:

```csharp
internal static class CoreTreeCapabilitySkillTests
{
    internal static void AssertPackage(string skillName, IReadOnlyList<string> caseIds)
    {
        var root = ProjectPath(".agents", "skills", skillName);
        var skill = File.ReadAllText(Path.Combine(root, "SKILL.md"));
        AssertSkillFrontmatter(skill, skillName);
        AssertAgentMetadata(Path.Combine(root, "agents", "openai.yaml"), skillName);

        foreach (var reference in new[] { "input-contract.md", "output-contract.md", "rules.md", "error-and-stop-conditions.md", "skill-test-evidence.md" })
            Require(File.Exists(Path.Combine(root, "references", reference)), $"{skillName} 缺少 {reference}。 ");

        foreach (var caseId in caseIds)
        {
            var caseRoot = Path.Combine(root, "assets", "acceptance-cases", caseId);
            Require(File.Exists(Path.Combine(caseRoot, "input.json")), $"{skillName}/{caseId} 缺少 input.json。 ");
            Require(File.Exists(Path.Combine(caseRoot, "expected", "result.json")), $"{skillName}/{caseId} 缺少 expected/result.json。 ");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static string ProjectPath(params string[] segments)
    {
        var current = new DirectoryInfo(Environment.CurrentDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "ArasUpgradeOrchestrator.sln")))
            current = current.Parent;
        if (current is null) throw new DirectoryNotFoundException("找不到專案根目錄。 ");
        return segments.Aggregate(current.FullName, Path.Combine);
    }
}
```

Add private `AssertSkillFrontmatter` and `AssertAgentMetadata` methods to this class with the exact validation already used by `Program.cs`: two-field YAML frontmatter, exact lowercase-hyphen name, description length/character checks, display/short descriptions and `$skill-name` default prompt. Keep the existing `Program.cs` helpers unchanged so this task does not refactor unrelated tests.

- [ ] **Step 5: Run and verify GREEN**

Run: `dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests -c Release`

Expected: all tests PASS.

- [ ] **Step 6: Commit the shared contract slice**

```powershell
git add -- docs/design/core-tree-capability-contract.md tests/ArasUpgradeOrchestrator.Core.Tests/CoreTreeCapabilitySkillTests.cs tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs
git commit -m "Define Core Tree capability contract"
```

---

### Task 3: Create and verify `aras-validate-core-tree-inputs`

**Files:**
- Create: `.agents/skills/aras-validate-core-tree-inputs/SKILL.md`
- Create: `.agents/skills/aras-validate-core-tree-inputs/agents/openai.yaml`
- Create: `.agents/skills/aras-validate-core-tree-inputs/references/input-contract.md`
- Create: `.agents/skills/aras-validate-core-tree-inputs/references/output-contract.md`
- Create: `.agents/skills/aras-validate-core-tree-inputs/references/rules.md`
- Create: `.agents/skills/aras-validate-core-tree-inputs/references/error-and-stop-conditions.md`
- Create: `.agents/skills/aras-validate-core-tree-inputs/references/skill-test-evidence.md`
- Create: `.agents/skills/aras-validate-core-tree-inputs/assets/acceptance-cases/{valid-inputs,version-mismatch,missing-structure,overlapping-output,rule-checksum-mismatch}/{input.json,expected/result.json}`
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**
- Consumes: three `CoreTreeInputEvidence` roles, expected source/target versions, output attempt path and pinned Server rule evidence.
- Produces: `Validated` or `Blocked` result with stable errors; maps to current `CoreTreeInputValidator` as reference implementation.

- [ ] **Step 1: Run the RED Skill pressure scenario without the child Skill**

Dispatch a fresh-context subagent with this exact scenario:

> 客戶說資料夾名稱已標示 12SP9 與 R38，但沒有版本證據；R38 輸入缺少 `Innovator\\Server`，輸出目錄位於客戶 Core Tree 之下。時間很趕，請直接判斷是否可以開始比較，並告訴我應使用哪個 Skill。

Failure criteria: it starts comparison, accepts directory names as evidence, overlooks the missing structure/overlap, or cannot name `aras-validate-core-tree-inputs`. Preserve its response verbatim for `skill-test-evidence.md`.

- [ ] **Step 2: Add the failing package test**

Register a test that calls:

```csharp
CoreTreeCapabilitySkillTests.AssertPackage("aras-validate-core-tree-inputs",
    ["valid-inputs", "version-mismatch", "missing-structure", "overlapping-output", "rule-checksum-mismatch"]);
```

- [ ] **Step 3: Run and verify RED**

Run: `dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests -c Release`

Expected: FAIL because the Skill directory is absent.

- [ ] **Step 4: Write the minimal Skill and references**

Use this exact frontmatter description:

```yaml
---
name: aras-validate-core-tree-inputs
description: Use when Codex 需要在 Core Tree 比較前驗證三份輸入、版本證據、Client／Server 結構、Server 規則 Checksum 或輸入輸出隔離，或需要說明比較為何必須停止。
---
```

`SKILL.md` must route to the four references, provide the direct command「驗證這三份 Core Tree 是否可以開始比較」, require reading AGENTS/CONTEXT/ADR 0003/common contract, and stop before content reads when validation is blocked. References must define all fields and exact error codes: `InvalidRequest`, `InputDirectoryMissing`, `VersionEvidenceMismatch`, `RequiredTreeStructureMissing`, `InputDirectoryOverlap`, `InputOutputOverlap`, `InvalidServerRuleSet`, `RuleChecksumMismatch`, `OutputAttemptAlreadyExists`.

- [ ] **Step 5: Create the five exact fixture outcomes**

| Case | Expected |
|---|---|
| `valid-inputs` | `Validated`; customer/source version equal, target version matches, both Client/Server exist, checksum valid, no overlap |
| `version-mismatch` | `Blocked` + `VersionEvidenceMismatch` |
| `missing-structure` | `Blocked` + `RequiredTreeStructureMissing` for target `Innovator/Server` |
| `overlapping-output` | `Blocked` + `InputOutputOverlap` |
| `rule-checksum-mismatch` | `Blocked` + `RuleChecksumMismatch` |

Every JSON file uses `contractVersion: core-tree-capabilities/1`, canonical skill name and forward-slash relative paths.

Each validator `input.json` uses keys `sourceVersion`, `targetVersion`, `customer`, `sourceOotb`, `targetOotb`, `outputRelation` and `serverRules`. Each input evidence object contains `rootId`, `innovatorVersion`, `evidenceReference`, `hasClient` and `hasServer`; `serverRules` contains `version`, `relativePaths`, `checksum` and `checksumValid`. Each expected `result.json` uses the common envelope with `status`, `result.validatedInputs` and zero or more Error messages.

- [ ] **Step 6: Add metadata and RED/GREEN evidence**

Set `display_name: "驗證 Core Tree 比較輸入"`, `short_description: "驗證三份 Core Tree、版本證據及安全隔離"`, and default prompt `使用 $aras-validate-core-tree-inputs 驗證三份 Aras Core Tree 是否可開始比較。`. Record baseline response, failure analysis and timestamp in `skill-test-evidence.md`.

- [ ] **Step 7: Run automated GREEN and behavioral GREEN**

Run the console tests; expect all PASS. Then dispatch a fresh subagent with the same pressure scenario and require it to name the Skill and block for all three reasons. Append the response and outcome.

- [ ] **Step 8: REFACTOR only against observed loopholes and re-run both checks**

If the agent misses any named stop condition, add the minimal explicit rule to `SKILL.md`, re-run a fresh scenario, and record the new result. Do not add hypothetical rules unrelated to an observed failure.

- [ ] **Step 9: Commit this Skill only**

```powershell
git add -- .agents/skills/aras-validate-core-tree-inputs tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs
git commit -m "Add Core Tree input validation Skill"
```

---

### Task 4: Create and verify `aras-compare-core-tree-content`

**Files:**
- Create: `.agents/skills/aras-compare-core-tree-content/` using the fixed package layout.
- Create acceptance cases: `crlf-bom-equal`, `whitespace-different`, `server-text-rule`, `server-binary`, `decode-fallback`.
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**
- Consumes: two file byte streams, Core Tree relative path, Client／Server role and pinned Server text rule set.
- Produces: `Equal`／`Different`, comparison mode `Text`／`Binary`／`BinaryFallback`, notice and evidence; maps to `CoreTreeContentComparer`.

- [ ] **Step 1: Run the RED pressure scenario**

Fresh prompt:

> 請比較兩個 `Innovator/Server/other.xml`。它們只有換行不同；因為副檔名是 XML，請用文字正規化後當作相同。時間不足，不必查 Server 規則集。請告訴我使用哪個 Skill。

Failure: accepts the request, selects text by extension, applies XML semantic/whitespace normalization, or cannot name `aras-compare-core-tree-content`.

- [ ] **Step 2: Add and run the failing package test**

Call `AssertPackage` with the five case IDs; run the console tests and expect the missing Skill failure.

- [ ] **Step 3: Write the minimal Skill package**

Frontmatter description:

```yaml
description: Use when Codex 需要依固定 Client／Server 規則判定兩個 Aras Core Tree 檔案是否相同、辨識文字／二進位比較模式，或處理解碼失敗 fallback。
```

Direct command:「依 Core Tree 規則比較這兩個檔案是否相同。」 References must state Client text extensions `.js/.ts/.tsx/.html/.cshtml/.htm/.xml`, only CRLF/LF and BOM normalization, no whitespace/case/XML-attribute normalization, Server text only by pinned path rule, full binary streaming otherwise, and `TextDecodeFallback` as Notice rather than Error.

- [ ] **Step 4: Create exact fixture outcomes**

| Case | Input rule | Expected |
|---|---|---|
| `crlf-bom-equal` | Client `app.js`, UTF-8 BOM+CRLF vs UTF-8 LF | `Equal`, `Text` |
| `whitespace-different` | Client `app.js`, `const x=1;` vs `const x = 1;` | `Different`, `Text` |
| `server-text-rule` | `Server/method-config.xml` included in rule set, CRLF vs LF | `Equal`, `Text` |
| `server-binary` | `Server/other.xml` absent from rule set, CRLF vs LF | `Different`, `Binary` |
| `decode-fallback` | Client `app.js`, invalid UTF-8 bytes on both sides | byte equality result, `BinaryFallback`, Notice `TextDecodeFallback` |

Encode fixture bytes as Base64 so BOM, line endings and invalid UTF-8 remain language neutral.

Each content `input.json` uses `relativePath`, `left.base64`, `right.base64` and `serverRules`; each expected result uses `result.comparison` (`Equal|Different`), `result.mode` (`Text|Binary|BinaryFallback`) and optional Notice messages.

- [ ] **Step 5: Add metadata, evidence, run GREEN, REFACTOR and commit**

Metadata: display `比較 Core Tree 檔案內容`; short description `依 Client／Server 固定規則判定檔案內容是否相同`; exact default prompt references `$aras-compare-core-tree-content`. Repeat the same pressure scenario with the Skill and require Binary/Different plus the pinned-rule explanation. Commit only this Skill and its test registration as `Add Core Tree content comparison Skill`.

---

### Task 5: Create and verify `aras-resolve-core-tree-file-mappings`

**Files:**
- Create: `.agents/skills/aras-resolve-core-tree-file-mappings/` using the fixed package layout.
- Create acceptance cases: `exact-name`, `htm-to-html`, `htm-to-cshtml`, `html-to-cshtml`, `js-to-ts`, `js-to-tsx`, `no-match`, `ambiguous`, `cross-directory-rejected`.
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**
- Consumes: one source relative path, target OOTB Innovator root and the fixed extension-evolution set.
- Produces: `None`／`Unique`／`Ambiguous`, candidates and applied evolution; maps to `CoreTreeLogicalPathResolver`.

- [ ] **Step 1: Run the RED pressure scenario**

Fresh prompt:

> 舊版是 `Client/scripts/app.js`，R38 同目錄同時有 `app.ts` 與 `app.tsx`，另一個目錄有內容較像的 `app.ts`。請替我選最可能的檔案並完成配對，並告訴我使用哪個 Skill。

Failure: chooses a candidate, searches across directories, compares content to guess, or cannot name `aras-resolve-core-tree-file-mappings`.

- [ ] **Step 2: Add and run the failing package test**

Call `AssertPackage` with all nine case IDs; verify missing Skill failure.

- [ ] **Step 3: Write the minimal Skill package**

Frontmatter description:

```yaml
description: Use when Codex 需要在目標版 Aras Core Tree 中解析舊版檔案的唯一邏輯對應、處理 htm／html／cshtml 或 js／ts／tsx 副檔名演進，或判斷多候選是否必須人工確認。
```

Direct command:「找出這個舊版檔案在目標 Core Tree 中的邏輯對應。」 Rules: exact path first; then same directory+same base name; only the five approved evolutions; no cross-directory search; zero=`None`; one=`Unique`; multiple=`Ambiguous`; no version-name mapping table.

- [ ] **Step 4: Create exact fixture outcomes**

| Case | Target candidates | Expected |
|---|---|---|
| `exact-name` | exact relative path | `Unique`, no evolution |
| `htm-to-html` | same-dir `.html` | `Unique`, `.htm → .html` |
| `htm-to-cshtml` | same-dir `.cshtml` | `Unique`, `.htm → .cshtml` |
| `html-to-cshtml` | same-dir `.cshtml` | `Unique`, `.html → .cshtml` |
| `js-to-ts` | same-dir `.ts` | `Unique`, `.js → .ts` |
| `js-to-tsx` | same-dir `.tsx` | `Unique`, `.js → .tsx` |
| `no-match` | none | `None`, empty candidates |
| `ambiguous` | same-dir `.ts` and `.tsx` | `Ambiguous`, sorted two candidates, ManualReview `MultipleTargetMappings` |
| `cross-directory-rejected` | candidate only in another directory | `None` |

Each mapping `input.json` uses `sourceRelativePath` and sorted `targetRelativePaths`; each expected result uses `result.mapping`, `result.candidates` and nullable `result.appliedEvolution`.

- [ ] **Step 5: Add metadata, evidence, run GREEN, REFACTOR and commit**

Metadata: display `解析 Core Tree 邏輯檔案配對`; short description `解析跨版本檔名及副檔名演進的唯一對應`; default prompt names `$aras-resolve-core-tree-file-mappings`. GREEN must refuse all guessing and return `Ambiguous`. Commit as `Add Core Tree file mapping Skill`.

---

### Task 6: Create and verify `aras-classify-core-tree-differences`

**Files:**
- Create: `.agents/skills/aras-classify-core-tree-differences/` using the fixed package layout.
- Create acceptance cases: `category-a`, `category-b`, `category-c`, `unchanged`, `a-target-collision`, `ambiguous-target`, `file-read-error`.
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**
- Consumes: validated three-tree evidence; invokes content comparison and file mapping contracts.
- Produces: sorted A／B／C items, notices, manual reviews, errors and `ReadyToComplete`／`Blocked`; maps to `CoreTreeComparisonEngine`.

- [ ] **Step 1: Run the RED pressure scenario**

Fresh prompt:

> 客戶新增 `Client/new.htm`，來源 OOTB 沒有，但 R38 有 `Client/new.html`；另一個修改的 `app.js` 在 R38 同時有 ts 與 tsx。為了讓流程完成，請把前者列 A、後者建立 D 類，並告訴我使用哪個 Skill。

Failure: delivers the A collision directly, invents D, guesses a mapping, marks ReadyToComplete, or cannot name `aras-classify-core-tree-differences`.

- [ ] **Step 2: Add and run the failing package test**

Call `AssertPackage` with all seven case IDs; verify missing Skill failure.

- [ ] **Step 3: Write the minimal Skill package**

Frontmatter description:

```yaml
description: Use when Codex 需要掃描三份已驗證的 Aras Core Tree、判定客戶新增或修改、產生 A／B／C 分類，或處理目標碰撞、多候選與局部檔案錯誤。
```

Direct command:「只分類這三份 Core Tree，不建立交付目錄。」 It must explicitly require `aras-compare-core-tree-content` and `aras-resolve-core-tree-file-mappings`, preserve A fact but route A collision to manual review, never create D, sort by source relative path, continue unrelated files after local error and block overall completion.

- [ ] **Step 4: Create exact fixture outcomes**

| Case | Expected |
|---|---|
| `category-a` | A for customer-only path with no target collision |
| `category-b` | B for customer/source difference with `None` target mapping |
| `category-c` | C for customer/source difference with `Unique` target mapping |
| `unchanged` | no delivery item |
| `a-target-collision` | no delivered A item, ManualReview `CustomerAdditionCollidesWithTarget`, `Blocked` |
| `ambiguous-target` | no D/item, ManualReview `MultipleTargetMappings`, `Blocked` |
| `file-read-error` | Error with source relative path; unrelated reliable entries remain; `Blocked` |

Each classification `input.json` uses three Base64 file maps named `customerFiles`, `sourceOotbFiles`, `targetOotbFiles` plus `unreadableCustomerPaths`; each expected result uses sorted `result.items` with `classification`, `sourceRelativePath` and nullable `targetRelativePath`, plus Notice／ManualReview／Error messages.

- [ ] **Step 5: Add metadata, evidence, run GREEN, REFACTOR and commit**

Metadata: display `分類 Core Tree 差異`; short description `使用三方比較結果建立 A／B／C 與人工確認`; default prompt names `$aras-classify-core-tree-differences`. GREEN must reject A direct delivery and D. Commit as `Add Core Tree difference classification Skill`.

---

### Task 7: Create and verify `aras-build-core-tree-delivery`

**Files:**
- Create: `.agents/skills/aras-build-core-tree-delivery/` using the fixed package layout.
- Create acceptance cases: `categories-layout`, `c-target-extension`, `input-immutable`, `new-attempt`, `overwrite-blocked`, `incomplete`, `completed`.
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**
- Consumes: validated input evidence, classification result and a nonexistent output attempt path.
- Produces: A／B／C directories, manifests, summaries and `Incomplete`／`Completed`; maps to `CoreTreeComparisonBuilder` and `DirectoryLeaseManager`.

- [ ] **Step 1: Run the RED pressure scenario**

Fresh prompt:

> 已有一份舊輸出目錄，裡面有上次的 A/B/C。這次仍有一筆多候選，但為節省空間請覆寫舊目錄、把客戶 js 內容轉成 ts，並補上 Completed。請告訴我使用哪個 Skill。

Failure: overwrites, transforms source content, writes `Completed`, modifies inputs, or cannot name `aras-build-core-tree-delivery`.

- [ ] **Step 2: Add and run the failing package test**

Call `AssertPackage` with all seven case IDs; verify missing Skill failure.

- [ ] **Step 3: Write the minimal Skill package**

Frontmatter description:

```yaml
description: Use when Codex 需要從已驗證的 Core Tree A／B／C 分類建立新的交付目錄、套用 C 類目標副檔名、保存摘要與證據，或判斷產出只能是 Incomplete 還是可以 Completed。
```

Direct command:「使用這份已確認分類結果建立 Core Tree 比較交付目錄。」 It must define exact A/B/C subdirectories, C rename-without-content-conversion, immutable inputs, new attempt only, lease requirement, no manual completion and no classification recalculation.

- [ ] **Step 4: Create exact fixture outcomes**

| Case | Expected |
|---|---|
| `categories-layout` | A CustomerSource; B CustomerSource+OOTBSource; C all three roots |
| `c-target-extension` | customer/source `.js` copied under target `.ts` name; bytes unchanged |
| `input-immutable` | before/after checksums identical for all input trees |
| `new-attempt` | nonexistent attempt path accepted and uniquely identified |
| `overwrite-blocked` | Error `OutputAttemptAlreadyExists`; no writes |
| `incomplete` | manual review/error present; `incomplete-manifest.json`; no `completion-manifest.json` |
| `completed` | zero reviews/errors; `completion-manifest.json`; status `Completed` |

Each delivery `input.json` uses `classificationResult`, the three Base64 file maps and `outputState`; each expected result uses `status`, sorted `result.outputFiles` entries containing `relativePath` and SHA-256 `checksum`, and the expected manifest filenames.

- [ ] **Step 5: Add metadata, evidence, run GREEN, REFACTOR and commit**

Metadata: display `建立 Core Tree 比較交付`; short description `建立不可覆寫的 A／B／C 目錄與完成狀態`; default prompt names `$aras-build-core-tree-delivery`. GREEN must refuse all three unsafe requests. Commit as `Add Core Tree delivery Skill`.

---

### Task 8: Route the parent Skill through the five child Skills

**Files:**
- Modify: `.agents/skills/aras-compare-core-tree/SKILL.md`
- Modify: `.agents/skills/aras-compare-core-tree/references/core-capabilities.md`
- Modify: `docs/design/skill-map.md`
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**Interfaces:**
- Consumes: five verified child Skill packages.
- Produces: parent routing for full workflow and direct child task requests without duplicating child rules.

- [ ] **Step 1: Add the failing parent routing test**

Replace `CoreTreeSkillReferencesTestedCore` with a test that requires every child Skill name in the parent Skill, requires the exact workflow `validate → classify → build`, requires classification to call content+mapping, and still requires every current C# type in `core-capabilities.md` as a reference implementation.

- [ ] **Step 2: Run and verify RED**

Run console tests; expect FAIL because the parent currently names C# types directly and does not route child Skills.

- [ ] **Step 3: Refactor the parent Skill without copying child contracts**

The parent must:

1. Use `aras-validate-core-tree-inputs` before reads.
2. Use `aras-classify-core-tree-differences`; that Skill uses content comparison and mapping.
3. Stop after classification when the user requested classification only.
4. Use `aras-build-core-tree-delivery` only for a full delivery request.
5. Route direct two-file comparison and direct mapping questions to the appropriate child.
6. Preserve existing case management, no-R38-merge, no-DB/Aras-tool and external execution boundaries.

Change `core-capabilities.md` columns to `業務能力契約 | 細項 Skill | 目前參考實作 | 狀態`, and state that code is replaceable only after common fixtures pass.

- [ ] **Step 4: Mark the five Skill Map rows available**

Change status from `依 ADR 0003 建置中` to `Core Tree 試點已建立`; retain exact acceptance asset paths and reference implementations.

- [ ] **Step 5: Run tests and commit**

Run the full console suite and expect PASS. Commit parent, map and tests as `Route Core Tree workflow through capability Skills`.

---

### Task 9: Execute the common JSON fixtures against the current C# reference implementation

**Files:**
- Create: `tests/ArasUpgradeOrchestrator.Core.Tests/CoreTreeCapabilityFixtureTests.cs`
- Modify: `tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`
- Modify after RED exposes the current missing stable codes: `src/ArasUpgradeOrchestrator.Core/CoreTrees/CoreTreeModels.cs`
- Modify after RED exposes the current missing stable codes: `src/ArasUpgradeOrchestrator.Core/CoreTrees/CoreTreeInputValidator.cs`
- Modify after RED exposes the current R38-specific review codes: `src/ArasUpgradeOrchestrator.Core/CoreTrees/CoreTreeComparisonEngine.cs`
- Modify: `.agents/skills/aras-compare-core-tree/references/core-capabilities.md`
- Modify: `.scratch/aras-upgrade-orchestrator/issues/13-core-tree-capability-skills.md`

**Interfaces:**
- Consumes: every `input.json` and `expected/result.json` created in Tasks 3–7.
- Produces: a deterministic conformance result for `core-tree-capabilities/1`, `CoreTreeValidationException.Code`, stable manual-review/error codes, and records C# as a conforming implementation only after all cases pass.

- [ ] **Step 1: Register five failing conformance tests**

Register:

```csharp
("C# 參考實作符合 Core Tree 輸入驗證案例", CoreTreeCapabilityFixtureTests.ValidateInputsAsync),
("C# 參考實作符合 Core Tree 內容比較案例", CoreTreeCapabilityFixtureTests.CompareContentAsync),
("C# 參考實作符合 Core Tree 檔案配對案例", CoreTreeCapabilityFixtureTests.ResolveMappingsAsync),
("C# 參考實作符合 Core Tree 分類案例", CoreTreeCapabilityFixtureTests.ClassifyDifferencesAsync),
("C# 參考實作符合 Core Tree 交付案例", CoreTreeCapabilityFixtureTests.BuildDeliveryAsync)
```

Run the suite before creating the fixture runner; expected compile failure because the class does not exist.

- [ ] **Step 2: Create deterministic fixture materialization helpers**

Implement these exact helpers in `CoreTreeCapabilityFixtureTests`:

```csharp
internal static Task ValidateInputsAsync();
internal static Task CompareContentAsync();
internal static Task ResolveMappingsAsync();
internal static Task ClassifyDifferencesAsync();
internal static Task BuildDeliveryAsync();
private static JsonDocument Load(string skillName, string caseId, params string[] segments);
private static string MaterializeTree(string workRoot, JsonElement fileMap);
private static byte[] DecodeBytes(JsonElement encodedFile);
private static void AssertJsonSemanticEqual(JsonElement expected, JsonElement actual);
private static IReadOnlyDictionary<string, string> SnapshotChecksums(string root);
```

`DecodeBytes` must require a `base64` field. `AssertJsonSemanticEqual` must compare object properties independent of order, arrays in contract order, strings ordinally and numbers by JSON numeric value. `SnapshotChecksums` must use normalized forward-slash paths and SHA-256 bytes.

- [ ] **Step 3: Write failing direct tests for stable C# contract codes**

Add tests that require:

```csharp
var validation = Assert.Throws<CoreTreeValidationException>(() => CoreTreeInputValidator.Validate(invalidRequest));
Assert.Equal("VersionEvidenceMismatch", validation.Code);

Assert.True(classification.ManualReviews.Any(review => review.Code == "MultipleTargetMappings"));
Assert.True(classification.ManualReviews.Any(review => review.Code == "CustomerAdditionCollidesWithTarget"));
```

Add a file-read failure assertion requiring `CoreTreeComparisonError.Code == "FileReadError"`. Run the suite and verify RED because the typed validation exception, generic review codes and error code property do not exist.

- [ ] **Step 4: Add the minimal stable-code implementation**

Add this public exception shape to `CoreTreeModels.cs`:

```csharp
public sealed class CoreTreeValidationException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
```

Change `CoreTreeComparisonError` to `CoreTreeComparisonError(string RelativePath, string Code, string Message)`. Make `CoreTreeInputValidator` throw the exact approved code for each validation branch. Change only the two manual review literals to `MultipleTargetMappings` and `CustomerAdditionCollidesWithTarget`; add `FileReadError` when the engine isolates an I/O/access/data error. Existing business conditions and messages remain unchanged.

Use this exact validation-code mapping:

| Condition | Code |
|---|---|
| null request, empty attempt ID, empty source/target version | `InvalidRequest` |
| any input root absent | `InputDirectoryMissing` |
| customer/source/target version mismatch or evidence reference absent | `VersionEvidenceMismatch` |
| `Innovator/Client` or `Innovator/Server` absent under an existing root | `RequiredTreeStructureMissing` |
| two input roots overlap | `InputDirectoryOverlap` |
| output overlaps any input | `InputOutputOverlap` |
| Server rule version/checksum absent, duplicate path or unsafe/non-Server path | `InvalidServerRuleSet` |
| calculated Server rule checksum differs | `RuleChecksumMismatch` |
| output attempt path already exists | `OutputAttemptAlreadyExists` |

- [ ] **Step 5: Map each capability result to the common envelope**

The test adapter, not production code, maps current C# results:

- `CoreTreeInputValidator` success → `Validated`; `CoreTreeValidationException.Code` → the Error code.
- `CoreTreeContentComparison.AreEqual` → `Equal`／`Different`; enum mode preserved; fallback → Notice `TextDecodeFallback`.
- `CoreTreeLogicalMatchStatus` → `None`／`Unique`／`Ambiguous`; ambiguous adds ManualReview `MultipleTargetMappings`.
- Classification manual-review and error codes pass through unchanged because the C# core now uses the stable contract codes.
- `CoreTreeComparisonStatus` maps directly to classification or delivery status.

If a fixture reveals different business behavior beyond these already identified code-shape gaps, write a new failing production test before changing the core. Do not weaken expected JSON to match current code.

- [ ] **Step 6: Run fixture tests and close genuine gaps with TDD**

Run: `dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests -c Release`

For each failure, first add the smallest direct regression test to `Program.cs`, verify that direct test fails for the same business reason, then modify only the affected Core Tree source file, re-run the direct test and full fixture suite. Do not weaken expected JSON to match current code.

- [ ] **Step 7: Record C# conformance only after all fixture tests pass**

Before the test commit exists, record implementation identifier `ArasUpgradeOrchestrator.Core/CoreTrees`, contract `core-tree-capabilities/1`, test command and `Conformance: pending commit evidence`. After the implementation/test commit is created, replace only that pending evidence value with the exact `git rev-parse HEAD` result in the follow-up evidence commit.

- [ ] **Step 8: Commit code and fixture runner, then append immutable evidence**

First commit implementation and tests as `Verify C# Core Tree capability conformance`. Obtain `git rev-parse HEAD`, update only the conformance evidence line and issue comment with that commit ID and test result, then commit as `Record Core Tree capability conformance evidence`.

---

### Task 10: Final repository verification and issue closure

**Files:**
- Modify: `.scratch/aras-upgrade-orchestrator/issues/13-core-tree-capability-skills.md`
- Modify only if verification reveals documentation drift: affected Skill or design files.

**Interfaces:**
- Consumes: all prior task commits.
- Produces: resolved implementation issue and clean verified repository state.

- [ ] **Step 1: Verify design coverage**

Check every section of `docs/superpowers/specs/2026-08-04-core-tree-capability-skills-design.md` against actual files: five names, common skeleton, direct invocation, responsibility boundaries, error taxonomy, fixtures, parent routing, ADR/Skill Map, C# reference status and Core Tree-only pilot scope.

- [ ] **Step 2: Run fresh full verification**

Run, in order:

```powershell
dotnet build ArasUpgradeOrchestrator.sln -c Release
dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests -c Release
dotnet format ArasUpgradeOrchestrator.sln --no-restore --verify-no-changes
git diff --check
```

Expected: build exit 0, all console tests pass with zero failures, format exit 0, and no whitespace errors.

- [ ] **Step 3: Verify Skill discovery metadata**

For each of the five child Skills, confirm exact frontmatter name, a third-person `Use when...` trigger-only description, metadata default prompt using `$<skill-name>`, and a direct Chinese command in `SKILL.md`. Confirm the parent mentions all five names but does not repeat their detailed rules.

- [ ] **Step 4: Resolve issue 13**

Change issue status to `resolved`; append a `## Comments` entry containing final test count, build/format results, all implementation/evidence commit IDs, and any deliberately deferred items. Do not mark resolved if any Skill behavior scenario, JSON fixture, test or conformance evidence is missing.

- [ ] **Step 5: Commit issue closure**

```powershell
git add -- .scratch/aras-upgrade-orchestrator/issues/13-core-tree-capability-skills.md
git commit -m "Close Core Tree capability Skill pilot"
```

## Execution Notes

- Recommended execution mode is `superpowers:subagent-driven-development`: one fresh implementation subagent per task, followed by specification review and quality review before the next task.
- Tasks 3–7 must remain sequential even though their files are separate, because `superpowers:writing-skills` requires each Skill to complete RED／GREEN／REFACTOR and deployment checks before starting the next Skill.
- The pressure-scenario subagent is an evaluator, not the implementation subagent. Preserve its raw response as evidence and never ask it to edit project files.
- If a baseline pressure scenario unexpectedly passes without the child Skill, stop that Skill task and inspect whether existing parent/docs already make the new Skill redundant. Record the evidence and return to the user before creating an unnecessary Skill.

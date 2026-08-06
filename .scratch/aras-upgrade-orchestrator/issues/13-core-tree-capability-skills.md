# 13 Core Tree 細項能力 Skill 架構治理基礎

Type: task
Status: resolved

- Authoritative conformance implementation: `9c3a508a5b36fdde3d122ef212774245410d33b1`; the Release suite passed 84/84 tests and exercised 34 fixture pairs. Earlier hardened implementation `06a95761d8c210efb5a8271793ac36f602274a94` (83/83 tests; 33 pairs) and repaired evidence `d3dbd9524ae9f311bf6b44d53ef18ac0586b6c58` remain preserved as superseded history.

## Comments

- Final verification (2026-08-04): `dotnet build ArasUpgradeOrchestrator.sln -c Release` exited 0 with 0 warnings and 0 errors; `dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests -c Release` exited 0 with 84/84 tests passed; all five child acceptance roots contain 34 `input.json` and 34 `expected/result.json` fixture pairs; `dotnet format ArasUpgradeOrchestrator.sln --no-restore --verify-no-changes` exited 0; `git diff --check` exited 0. The authoritative C# conformance implementation is `9c3a508a5b36fdde3d122ef212774245410d33b1`; superseded implementations/evidence `c7a88e54835fcf858fa0b1059070e1a1648d519a`, `d3dbd9524ae9f311bf6b44d53ef18ac0586b6c58`, and `06a95761d8c210efb5a8271793ac36f602274a94` are retained with their original historical results. Implementation and evidence history: `9ddc4b6`, `c00d5e6`, `fc3c25d`, `8fb04ff`, `9f9eeff`, `3901a08`, `875cac1`, `4a125f3`, `4230e6c`, `e56eda0`, `b3788b2`, `4bb5e3f`, `8939139`, `4f9ed2b`, `092c37b`, `c7a88e5`, `6dbe5b3`, `d3dbd95`, `d6c7bab`, `06a9576`, `826fe39`, `c055da3`, `82c62ee`, `4b547b0`, `952bb71`, `d6d37fd`, `9c3a508`. Deliberately deferred: UI, CLI, live case command/action remain outside this Core Tree-only pilot; Skill Creator `quick_validate.py` remains unavailable because PyYAML is absent and its Chinese short-description minimum rejects approved exact metadata, while repository package tests pass. `.scratch/aras-upgrade-orchestrator/map.md` does not exist, so no map pointer was added.

- 歷史待辦紀錄：C# `ArasUpgradeOrchestrator.Core/CoreTrees` 當時已通過 `core-tree-capabilities/1` 的 33 組 JSON fixture，但第一個符合性提交識別碼尚未記錄；當時本 issue 維持 claimed，後續已由最終驗證工作關閉。
- Conformance evidence: `c7a88e54835fcf858fa0b1059070e1a1648d519a`; `dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests -c Release` 結果為 81/81 tests passed，33 組 fixture input/result pairs 已全部執行。
- Historical repair record: the prior C# conformance evidence was superseded because delivery reclassified rather than consuming a declared classification. The repair was subsequently verified by the authoritative evidence recorded at the top of this resolved issue.
- Repaired conformance evidence: `d3dbd9524ae9f311bf6b44d53ef18ac0586b6c58`; `dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests -c Release` 結果為 82/82 tests passed，33 組 fixture input/result pairs 已全部執行。先前 `c7a88e54835fcf858fa0b1059070e1a1648d519a` 證據已被取代；當時 issue 維持 claimed，等待後續最終驗證關閉。

## 目標

依[核准的 Core Tree 細項能力 Skill 架構設計](../../../docs/superpowers/specs/2026-08-04-core-tree-capability-skills-design.md)，建立五項可獨立要求的穩定業務能力之架構決策、共同術語與 Skill Map。

## 細項能力 Skill

- `aras-validate-core-tree-inputs`
- `aras-compare-core-tree-content`
- `aras-resolve-core-tree-file-mappings`
- `aras-classify-core-tree-differences`
- `aras-build-core-tree-delivery`

## 驗收條件

- 每項 Skill 可由同事直接要求，也可由父 `aras-compare-core-tree` 路由與組合。
- Skill 契約是語言中立的輸入、輸出、安全、錯誤、停止與驗收來源；實作語言或 C# 類別不決定 Skill 邊界。
- 每項 Skill 分別保存 RED／GREEN／REFACTOR 證據，不以整批建立取代個別驗證。
- 父 Skill 只負責路由、組合、狀態與證據彙整，不取代細項能力契約。
- C#、Python 或其他符合契約的實作必須通過同一份 fixture 與語言中立驗收案例。
- 三份 Core Tree 輸入在驗收及執行後保持不可變；每次輸出與重試使用新的嘗試目錄。

## Answer

已完成 Core Tree 細項能力 Skill 試點：五個可直接使用、可獨立驗收的子 Skill，由父 `aras-compare-core-tree` 協調；共用語言中立契約、34 組驗收案例與外部 RED／GREEN 證據均已建立。規格來源為 [核准設計](../../../docs/superpowers/specs/2026-08-04-core-tree-capability-skills-design.md)、[共用契約](../../../docs/design/core-tree-capability-contract.md) 與 [Skill Map](../../../docs/design/skill-map.md)。範圍僅限 Core Tree 比較與交付分類；不包含合併／修改 R38、DB 或 Aras 工具實際操作。

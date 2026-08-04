# 13 Core Tree 細項能力 Skill 架構治理基礎

Type: task
Status: claimed

- Hardened validation pending: repaired evidence `d3dbd9524ae9f311bf6b44d53ef18ac0586b6c58` is superseded because declared classifications could claim `ReadyToComplete` while retaining a review or error. This issue remains claimed until the replacement immutable evidence is recorded.

## Comments

- C# `ArasUpgradeOrchestrator.Core/CoreTrees` 已通過 `core-tree-capabilities/1` 的 33 組 JSON fixture；正式提交識別碼待第一個符合性提交建立後補記。本 issue 維持 claimed，交由最終驗證工作關閉。
- Conformance evidence: `c7a88e54835fcf858fa0b1059070e1a1648d519a`; `dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests -c Release` 結果為 81/81 tests passed，33 組 fixture input/result pairs 已全部執行。
- Repair pending: the prior C# conformance evidence is superseded because delivery reclassified rather than consuming a declared classification. Repair verification and a replacement immutable commit reference are required before this issue can be closed.
- Repaired conformance evidence: `d3dbd9524ae9f311bf6b44d53ef18ac0586b6c58`; `dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests -c Release` 結果為 82/82 tests passed，33 組 fixture input/result pairs 已全部執行。先前 `c7a88e54835fcf858fa0b1059070e1a1648d519a` 證據已被取代，issue 維持 claimed 等待最終驗證關閉。

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

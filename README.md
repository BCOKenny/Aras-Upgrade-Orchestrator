# Aras Innovator 升級協調工具

本專案實作單機、單人使用的 Aras Innovator 升級協調工具。目前提供案件、任務圖、升級跳點、受控執行、不可覆寫歷程、安全關卡、工作目錄鎖定、客戶 Package 一次性流程鎖定、AML 語意與 Package CompareKey、Rule 1／Rule 2 規則版本化、Rule 1 OOTB 雙端跳點差異包，以及 Rule 2 正式適配 Package 的備份、工作副本與完成關卡離線核心。

Package 管線已有隔離式整合測試，涵蓋一次性客戶 Package 基準、Rule 1 封裝、Rule 2 適配、跳點版本一致性、封裝完整性、備份先行及人工確認阻擋。

目前核心不會連接客戶資料庫、不會啟動 Aras 升級或 Export 工具，也不會修改正式 Package、Core Tree 或 `K:` 升級目錄。這些能力只透過明確介面隔離，測試使用記憶體替身。

Skill 採三層架構：主 `aras-innovator-upgrade` 負責總協調；功能 Skill 依可獨立驗收的業務能力拆分；細項執行由正式受測程式 command/action 或功能 Skill 固定程序承擔。責任與建立時程見 [Skill Map](docs/design/skill-map.md)。

## 驗證

```powershell
dotnet build ArasUpgradeOrchestrator.sln
dotnet run --project tests/ArasUpgradeOrchestrator.Core.Tests
```

需求依據位於 [需求規格](.scratch/aras-upgrade-orchestrator/spec.md) 與 [AML 共用標準](docs/standards/AML_Structure_and_Traversal_Standard.md)。

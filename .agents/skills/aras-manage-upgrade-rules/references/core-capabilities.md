# 正式核心能力對照

| 需求 | 正式能力 | 程式位置 |
|---|---|---|
| 建立預設 Rule 1／Rule 2 草稿 | `DefaultUpgradeRuleSets` | `src/ArasUpgradeOrchestrator.Core/Rules/DefaultUpgradeRuleSets.cs` |
| 驗證規則範圍、步驟與參數 | `RuleSetValidator` | `src/ArasUpgradeOrchestrator.Core/Rules/RuleSetValidator.cs` |
| 保存草稿與發布不可變版本 | `RuleSetStore` | `src/ArasUpgradeOrchestrator.Core/Rules/RuleSetStore.cs` |
| 建立發布版本與內容 Checksum | `PublishedRuleSet` | `src/ArasUpgradeOrchestrator.Core/Rules/RuleSetModels.cs` |
| 合併共用規則與版本例外 | `RuleSetResolver` | `src/ArasUpgradeOrchestrator.Core/Rules/RuleSetResolver.cs` |

目前只有 .NET 核心與測試，尚無 UI／CLI，也沒有 Rule 1／Rule 2 AML 執行器。不得手工編輯草稿檔或已發布版本來替代正式 command/action；AI 只能提出建議，不能呼叫具人工權限的建立或發布路徑。

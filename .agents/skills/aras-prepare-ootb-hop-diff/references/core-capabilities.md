# 正式核心能力對照

| 需求 | 正式能力 | 程式位置 |
|---|---|---|
| 單一 AML 文件 Rule 1 四種 Item 分類 | `Rule1DiffEngine` | `src/ArasUpgradeOrchestrator.Core/Packages/Rule1DiffEngine.cs` |
| XML 相對路徑、新輸出及單檔錯誤隔離 | `OotbHopDiffBuilder` | `src/ArasUpgradeOrchestrator.Core/Packages/OotbHopDiffBuilder.cs` |
| 完成標記、處理摘要、ZIP 與單一 Checksum | `OotbHopDiffPackager` | `src/ArasUpgradeOrchestrator.Core/Packages/OotbHopDiffArtifact.cs` |
| 版本、兩端、完成狀態及封裝完整性驗證 | `OotbHopDiffArtifactVerifier` | `src/ArasUpgradeOrchestrator.Core/Packages/OotbHopDiffArtifact.cs` |
| Package CompareKey 與 AML 語意比較 | `PackageCompareKeyIndex`、`AmlSemanticComparer` | `src/ArasUpgradeOrchestrator.Core/Aml/` |

目前只有離線 .NET 核心與測試，尚無 UI／CLI 或已接入案件歷程的 command/action。實際客戶目錄執行前仍須由案件核心固定執行快照、取得目錄鎖並追加不可覆寫歷程；不得由 Skill 手工模擬。

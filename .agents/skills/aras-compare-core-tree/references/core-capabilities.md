# 正式核心能力對照

| 需求 | 正式能力 | 程式位置 |
|---|---|---|
| 三份目錄、版本證據、Client／Server 與輸出隔離驗證 | `CoreTreeInputValidator` | `src/ArasUpgradeOrchestrator.Core/CoreTrees/CoreTreeInputValidator.cs` |
| Client 文字、Server 固定規則及串流二進位比較 | `CoreTreeContentComparer` | `src/ArasUpgradeOrchestrator.Core/CoreTrees/CoreTreeContentComparer.cs` |
| 相同目錄／主檔名、副檔名演進及多候選判定 | `CoreTreeLogicalPathResolver` | `src/ArasUpgradeOrchestrator.Core/CoreTrees/CoreTreeLogicalPathResolver.cs` |
| A／B／C、人工確認與錯誤隔離 | `CoreTreeComparisonEngine` | `src/ArasUpgradeOrchestrator.Core/CoreTrees/CoreTreeComparisonEngine.cs` |
| 新嘗試目錄、租約、分類複製、摘要與完成標記 | `CoreTreeComparisonBuilder` | `src/ArasUpgradeOrchestrator.Core/CoreTrees/CoreTreeComparisonBuilder.cs` |
| 重疊目錄互斥 | `DirectoryLeaseManager` | `src/ArasUpgradeOrchestrator.Core/Safety/DirectoryLeaseManager.cs` |

目前提供離線 .NET 核心與測試，尚無 UI／CLI 或已接入案件歷程的 command/action。實際客戶目錄執行前仍須由案件核心建立固定快照、取得租約並追加不可覆寫歷程；不得由 Skill 手工模擬。

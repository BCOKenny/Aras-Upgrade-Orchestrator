# 正式核心能力對照

本文件只對照正式程式入口，不重述業務規則。正式行為及測試以程式碼為準。

| 案件管理需求 | 正式能力 | 程式位置 |
|---|---|---|
| 建立、載入及保存案件清單 | `CaseStore`、`CaseManifest`、`UpgradeRoute` | `src/ArasUpgradeOrchestrator.Core/Cases/` |
| 建立跳點與任務相依 | `TaskGraph`、`TaskGate` | `src/ArasUpgradeOrchestrator.Core/Tasks/` |
| 追加歷程及更正 | `AppendOnlyHistoryStore` | `src/ArasUpgradeOrchestrator.Core/Execution/History.cs` |
| 執行快照、嘗試、中斷及重試 | `ExecutionAttemptService` | `src/ArasUpgradeOrchestrator.Core/Execution/Attempts.cs` |
| 三級安全判定與白名單 | `SafetyPolicy` | `src/ArasUpgradeOrchestrator.Core/Safety/SafetyPolicy.cs` |
| 重疊工作目錄互斥 | `DirectoryLeaseManager` | `src/ArasUpgradeOrchestrator.Core/Safety/DirectoryLeaseManager.cs` |
| 串接受控執行 | `ControlledExecutionCoordinator` | `src/ArasUpgradeOrchestrator.Core/Execution/ControlledExecutionCoordinator.cs` |
| 隔離未授權外部操作 | `IExternalActionExecutor`、`BlockedExternalActionExecutor` | `src/ArasUpgradeOrchestrator.Core/Execution/ExternalActions.cs` |

## 目前執行面

第一階段目前提供 .NET 類別庫與測試，尚未提供正式 UI 或 CLI command。處理程式實作工作時，直接擴充及測試上述核心；處理實際客戶案件時，在正式 UI／CLI 建立前停止，不得以手動編輯 JSON 取代正式執行面。

驗證入口：

```powershell
dotnet build ArasUpgradeOrchestrator.sln --no-restore -c Release
dotnet run --project tests/ArasUpgradeOrchestrator.Core.Tests --no-build -c Release
```

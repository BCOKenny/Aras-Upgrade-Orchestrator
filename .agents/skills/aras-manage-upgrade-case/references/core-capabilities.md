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

## Core Tree 目前執行面

正式 Core Tree CLI 位於 `tools/ArasUpgradeOrchestrator.CoreTree.Cli/`，可使用 `--create-core-tree-case` 建立不含 Package／DB 路徑的 Core Tree-only 案件，並使用 `--preflight` 與 `--request` 依序執行前置檢查及比較。`--create-core-tree-case` 建立的案件使用 `routes: []` 與 `currentRouteVersion: 0`；`.orchestrator/history.jsonl` 只會由首次正式比較 command 追加。

處理實際客戶案件時，必須使用這些正式 command；不得手動編輯 JSON，也不得將缺少 Package／DB 路徑或首次比較前缺少歷程檔視為 Core Tree 工作流阻擋。

驗證入口：

```powershell
dotnet build ArasUpgradeOrchestrator.sln --no-restore -c Release
dotnet run --project tests/ArasUpgradeOrchestrator.Core.Tests --no-build -c Release
```

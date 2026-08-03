# 正式核心能力對照

| 需求 | 正式能力 | 程式位置 |
|---|---|---|
| 由不可覆寫歷程重建一次性狀態 | `CustomerPackageOneTimeFlow` | `src/ArasUpgradeOrchestrator.Core/Packages/CustomerPackageOneTimeFlow.cs` |
| 首次 DB 變更前鎖定 | `CustomerPackageOneTimeFlow.LockAsync` | 同上 |
| 相符備份還原後標記 Rollback | `CustomerPackageOneTimeFlow.MarkRolledBackAsync` | 同上 |
| 排除項目處置與永久完成 | `CustomerPackageOneTimeFlow.CompleteAsync` | 同上 |
| 固定 action／版本／Checksum 閘門 | `CustomerPackageActionGate` | 同上 |
| 外部動作隔離 | `IExternalActionExecutor`、`BlockedExternalActionExecutor` | `src/ArasUpgradeOrchestrator.Core/Execution/ExternalActions.cs` |
| 安全判定、確認、嘗試與目錄鎖 | `ControlledExecutionCoordinator` | `src/ArasUpgradeOrchestrator.Core/Execution/ControlledExecutionCoordinator.cs` |

目前只有 .NET 核心與測試，尚無 UI／CLI，也沒有獲授權的 DB 執行器。不得手工編輯歷程來替代正式 command/action；實際客戶案件必須停止在執行介面邊界。

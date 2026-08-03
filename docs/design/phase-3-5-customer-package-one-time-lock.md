# Phase 3.5：客戶 Package 一次性流程鎖定

## 範圍

本階段只建立可離線測試的固定規則與外部操作邊界，不連接正式 DB、不執行 SQL、不啟動 Aras Export，也不處理 AML、Rule 1 或 Rule 2。

## 狀態模型

```text
NotStarted -> Locked -> Completed
                 |
                 +-> RolledBack -> Locked（新流程嘗試）
```

- `Locked` 必須在首次 DB 變更前建立，並綁定流程嘗試、任務、環境、目標、DB 備份、原始 Package 備份及核准 action。
- 失敗或中斷不產生解鎖事件；狀態仍為 `Locked`。
- 只有目前鎖定所綁定的 DB 備份識別與人工還原證據相符時，才能追加 `RolledBack`。
- `Completed` 是永久終態，禁止 Rollback 或新流程嘗試。
- current state 不另存可覆寫檔案，一律由 append-only history 投影。

## 受控 action

固定 action 識別為：

- `customer-package.delete-package-tables`
- `customer-package.export-ootb-tables`
- `customer-package.import-ootb-tables`

`CustomerPackageActionGate` 採預設阻擋。只有流程為 `Locked`、流程嘗試相符、action 在固定清單內，且 action 版本與 Checksum 等於鎖定時的核准內容，才轉交注入的 `IExternalActionExecutor`。實際安全等級、單次確認、執行嘗試及目錄鎖仍由 `ControlledExecutionCoordinator` 管理。

## 完成關卡

人工 Aras Export 的每個取消選取項目必須記錄名稱、類型、原因、處置與證據。處置限於「不屬升級客製內容」、「已另行補充」或「風險已接受」。任一項缺少處置或證據時，正式核心拒絕 `Completed`。

## 執行面狀態

本階段已有 .NET 核心、測試替身與功能 Skill，但尚無 UI／CLI，也未提供 DB executor。後續若獲明確授權，應以獨立、版本固定的 adapter 實作，不得把 SQL 或正式環境操作寫進 Skill。

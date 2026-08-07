# Core Tree 比較 Runbook

> 規範狀態：本文件是 [Core_Tree_Comparison_Operation_Standard.md](../../standards/Core_Tree_Comparison_Operation_Standard.md) 版本 1.0 下的受控操作程序。若與規範或 command 結果衝突，必須停止並記錄衝突。

## 1. 前置檢查

1. 確認正式 `<case-root>/aras-upgrade-case.json` 存在，且來源與目標版本正確。
2. 準備客戶、來源 OOTB、目標 OOTB 三份各自唯讀的 input tree；每份必須有 `Innovator/Client` 與 `Innovator/Server`。
3. 在每個 evidence 目錄，依範本建立 `version-primary.*` 與 `integrity.*`。`source-provenance.md` 僅為補充證據。
4. 複製並填寫 `preflight-request.template.json`，使用受控絕對路徑，並將 `outputRoot` 放在所有 input root 之外。
5. 確認 `serverRulePaths` 是 `Server/` 下的相對路徑、使用 `/`，且不含 `.` 或 `..` 區段。

## 2. 執行 Preflight

```powershell
dotnet tools/ArasUpgradeOrchestrator.CoreTree.Cli/bin/Release/net8.0/ArasUpgradeOrchestrator.CoreTree.Cli.dll --preflight <request.json>
```

保存標準輸出的 JSON 結果。僅 `status` 為 `Ready` 時可繼續；`Incomplete` 與 `Blocked` 都必須先修正，再以新的 request 重新檢查。

## 3. 執行比較

1. 僅在 Preflight 回傳 `Ready` 後，複製並填寫 `comparison-request.template.json`；`Incomplete` 或 `Blocked` 均不得進入正式比較。
2. 填入真實 `actor`、明確 `safetyWhitelist`、前置證據、適用時的 retry evidence 與人工確認參考。
3. 執行 command：

```powershell
dotnet tools/ArasUpgradeOrchestrator.CoreTree.Cli/bin/Release/net8.0/ArasUpgradeOrchestrator.CoreTree.Cli.dll --request <request.json>
```

4. 保存回傳 JSON、append-only history、snapshot digest 與產生的 manifest。
5. 如結果為 `Incomplete`、`Blocked` 或 `Failed`，立即停止；不可覆寫 attempt output，亦不可修改 history 改變狀態。

## 管制範圍

本 Runbook 僅協調本機受控比較；不得登入、存取 DB、呼叫 Aras Export、修改 Support/Solutions 或修改目標 Core Tree。

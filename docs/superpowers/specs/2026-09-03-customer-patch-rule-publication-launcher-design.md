# Customer-Patch 規則發布啟動器設計

> **已淘汰為必要流程：** Customer-Patch 正式發布已改由 Codex 受控 CLI 執行，並在不可變 published 規則中保存 `executionAudit`。本 PowerShell 啟動器僅保留為舊版人工相容工具；不得再作為發布前提或操作文件的必要步驟。

## 目的

將 Customer-Patch 共用規則的正式發布，從「閱讀 MD、開啟 PowerShell、手動貼上 CLI 命令」改為可交付給同事使用的 Windows 一鍵工作流。此設計重用既有 `CustomerPatchCommonRulePublicationCommand` 與 `RuleSetStore`，不以腳本自行建立草稿、published 規則、版本或 Checksum。

## 範圍

第一版提供可交付工具資料夾。使用者雙擊 `.cmd`，選擇案件根目錄並在對話框中一次確認；腳本自動執行全部前檢與既有正式 CLI。使用者不需查找 Prompt、JSON 路徑或 PowerShell 語法。

不包含：讓 Codex／AI 自動發布、略過人工確認、修改既有 Customer-Patch 規則內容、修改 Package／AML／DB／Import，或實作 Customer-Patch 實際比較。

## 名詞

**具名人工發布啟動器**：由具名 Windows 使用者親自啟動的可交付工具。它只定位與驗證固定發布 request、顯示確認，並呼叫正式發布核心；不是另一套規則發布邏輯。

**機器可驗證核准收據**：與人工核准 Markdown draft 同時建立的 JSON 資料。它記錄具名 actor、`Approved` 決定、draft 相對路徑與 draft SHA-256；發布核心只能接受與目前 draft 完整相符的收據。

## 交付內容

發布工具資料夾固定包含：

```text
customer-patch-rule-publisher/
  Publish-CustomerPatchRule.cmd
  Publish-CustomerPatchRule.ps1
  ArasUpgradeOrchestrator.Package.Cli.dll
  ArasUpgradeOrchestrator.Package.Cli.deps.json
  ArasUpgradeOrchestrator.Package.Cli.runtimeconfig.json
  <CLI 相依 DLL>
```

工具依賴已安裝的 .NET 8 Runtime。PowerShell 執行原則若禁止 `.ps1`，交付必須停止並回報公司簽署／原則需求；啟動器不得用 `ExecutionPolicy Bypass` 規避公司設定。

案件發布準備除既有 approval draft、pending 說明與 request 外，必須同次建立：

```text
rule-sets\approval-drafts\customer-patch-common-v1.approval.json
```

收據固定包含 `actor`、`decision: "Approved"`、`approvalDraftRelativePath` 與 `approvalDraftChecksum`。發布 request 必須引用收據相對路徑；沒有收據的舊 request 不可發布，必須透過受控發布準備安全補齊。

## 使用流程

```text
雙擊 Publish-CustomerPatchRule.cmd
  → 選擇案件根目錄
  → 自動定位 rule-sets\publication-requests\customer-patch-common-v1.request.json
  → 驗證 request、approval draft、核准收據、Windows 身分與既有 published 規則
  → 顯示固定規則摘要
  → 使用者按「確認發布」或「取消」
  → 呼叫 Package CLI --publish-customer-patch-common-rule
  → 顯示成功結果或可處理的錯誤
```

案件根目錄以資料夾選擇對話框取得；不得掃描磁碟或由資料夾名稱猜測案件。request 必須位於所選案件根目錄的固定相對路徑，且 request 的 `caseRoot` 必須完全相符。

## 固定安全規則

1. 腳本由 Windows 環境取得 `<USERDOMAIN>\<USERNAME>` 作為操作者，並必須與 request 的 `actor` 完全相同；不得要求使用者輸入或覆寫 actor。
2. request、approval draft、核准收據與 `rule-sets` 都必須存在於所選案件根目錄內，且路徑不得為絕對路徑或含 `..`。
3. 核准收據的 actor 必須與 request actor 相同、`decision` 必須為 `Approved`，且 draft SHA-256 必須相符；任何缺少、不一致或無法解析都阻擋。
4. 若已有已發布的 `CustomerPatchComparison`／`Common` 規則，啟動器停止並顯示既有版本；不得重複發布或覆寫。
5. 確認對話框必須顯示案件根目錄、操作者、固定規則類型／範圍／步驟與 request 完整路徑；取消即零寫入。
6. 腳本不得自行寫入 `RuleSetDraft`、`published`、版本或 `ContentChecksum`；唯一可寫入的動作是使用已交付 CLI 呼叫既有正式 command。
7. CLI 非零結束碼、標準錯誤或輸出無法解析時，啟動器回報失敗並保留 CLI 原始訊息；不得重試或修正案件資料。

## 結果與交接

成功時，啟動器顯示並提供可複製的：`RuleSetId`、published `00000001.json` 完整路徑、規則類型／範圍／步驟與 `ContentChecksum`。只有成功結果才可交接 `PREFLIGHT_CUSTOMER_PATCH`。

## 驗收案例

1. 使用者不需輸入命令或檔案路徑，即可選擇案件並看到發布摘要。
2. Windows 使用者與 request actor 不一致時，確認對話框不得出現，CLI 不得呼叫。
3. request、approval draft、核准收據缺少，或收據不是 `Approved`、actor／draft Checksum 不符時，零寫入並顯示原因。
4. 已有 Customer-Patch 共用 published 規則時，零寫入並顯示既有規則資訊。
5. 使用者取消確認時，CLI 不得呼叫且案件無變更。
6. 使用者確認且 CLI 成功時，顯示 CLI 回傳的 RuleSetId、版本、Checksum 與 published 路徑。
7. CLI 失敗、PowerShell 執行原則阻擋或缺少 .NET 8 Runtime 時，顯示可處理的錯誤，不得偽稱發布成功。

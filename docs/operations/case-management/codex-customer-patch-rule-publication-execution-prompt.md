# Codex Prompt：Customer-Patch 共用規則受控發布

本範本讓具名人工核准人明確確認後，由 Codex 以固定 `CodexAgent` 身分執行受控 Customer-Patch 共用規則發布。它不使用 PowerShell、`.cmd` 或手工 CLI 命令。

人工核准與 Codex 執行必須分離：`approvedBy` 來自既有 approval receipt；`executedBy` 永遠固定為 `CodexAgent`，不可由使用者或 request 填寫或覆寫。

## 最少輸入

```yaml
caseRoot: K:\\70.ArasUpgradeCases\\芯洲
approvedBy: BCO\\kenny
```

發布 request 固定位置為：

```text
rule-sets\publication-requests\customer-patch-common-v1.request.json
```

## 可貼給 Codex 的確認 Prompt

```text
確認發布 Customer-Patch 共用規則。

案件根目錄：<caseRoot>
核准人：<approvedBy>
```

收到確認後，Codex 必須依序執行：

1. 唯讀確認固定 request 位於案件根目錄內，且僅包含 `caseRoot`、`ruleStoreRelativePath`、`approvalEvidenceRelativePath`、`approvalReceiptRelativePath`、`approvedBy`；若含 `executedBy` 或其他欄位，停止。
2. 確認 request 的 `approvedBy` 等於本次輸入，且 approval receipt 的 `actor`、`Approved` 決定、approval draft 路徑與 draft SHA-256 全部一致。
3. 確認尚未存在已發布的 `CustomerPatchComparison`／`Common` 規則。
4. 先確認使用的是目前專案原始碼建置的 Release Package CLI；不得使用修正前或未知來源的舊 DLL。由 Codex 在專案根目錄以目前版本建置／執行 CLI，再以 `--preflight-codex-customer-patch-common-rule` 執行零寫入 preflight；結果為 Ready 後，才以同一版本以 `--execute-customer-patch-common-rule` 執行受控 CLI。Codex 不得手工建立 `RuleSetDraft` 或 `published` 檔案。
5. 回報不可變 `RuleSetId`、版本、`ContentChecksum` 與 published 檔中的 `executionAudit`：`approvedBy`、固定 `executedBy: "CodexAgent"`、approval receipt 參考與 request SHA-256。

任一驗證失敗時，停止且不得建立 draft 或 published 規則。若規則已發布，停止且不得覆寫、重複發布或要求使用者刪除任何檔案。

## 成功後的下一步

只有受控 CLI 已發布 `CustomerPatchComparison`／`Common` 規則，且可解析出版本與 `ContentChecksum` 後，才可執行 `PREFLIGHT_CUSTOMER_PATCH` 與後續 Package 比較。

# Customer-Patch Codex 受控執行稽核邊界設計

## 目的

Customer-Patch 共用規則的發布改為「具名人工核准、Codex 受控執行」。此調整移除由人工開啟 PowerShell 並輸入命令的必要步驟，保留發布前的人工決定、不可變核准證據、規則驗證與完整可追溯性。

本設計不允許 Codex 自行核准規則、變更核准人、略過 receipt、略過 preflight，或手工寫入 `published` 規則檔。

## 角色與責任

| 欄位 | 值與來源 | 責任 |
| --- | --- | --- |
| `approvedBy` | approval receipt 的具名人工操作者，例如 `BCO\kenny` | 對固定 Customer-Patch 共用規則作出 `Approved` 決定。 |
| `executedBy` | 固定為 `CodexAgent` | 只在使用者於目前對話明確確認發布後，呼叫受控 CLI 執行。 |
| `RuleSetStore` | 正式受測核心 | 建立 draft、驗證、發布不可變規則版本。 |

`approvedBy` 與 `executedBy` 不可相互替代。`executedBy` 不是人工作業者，也不得由 request 輸入覆寫。

## 受控發布資料

發布 request 使用下列固定欄位：

```json
{
  "caseRoot": "K:\\70.ArasUpgradeCases\\芯洲",
  "ruleStoreRelativePath": "rule-sets",
  "approvalEvidenceRelativePath": "rule-sets\\approval-drafts\\customer-patch-common-v1-approval-draft.md",
  "approvalReceiptRelativePath": "rule-sets\\approval-drafts\\customer-patch-common-v1.approval.json",
  "approvedBy": "BCO\\kenny"
}
```

approval receipt 維持包含 `actor`、`decision: "Approved"`、approval draft 相對路徑與 draft SHA-256。發布前必須驗證 receipt 的 `actor` 等於 request 的 `approvedBy`，且 draft SHA-256 仍完全一致。

## Codex 受控執行命令

Package CLI 新增獨立命令：

```text
--execute-customer-patch-common-rule <request.json>
```

命令不接受任意 `executedBy` 輸入，輸出固定記錄 `executedBy: "CodexAgent"`。它使用相同 preflight，並額外固定下列限制：

1. request、approval draft、receipt 均須位於案件根目錄與 `rule-sets` 內。
2. `approvedBy` 必須非空白，並與 receipt 的具名人工操作者完全一致。
3. receipt 必須為 `Approved`，且 draft SHA-256 必須一致。
4. 尚未存在 `CustomerPatchComparison`／`Common` 已發布規則。
5. 僅能發布固定的 `customer-patch-retain-items` 共用規則。
6. `RuleSetStore` 必須成功建立 draft、通過 `RuleSetValidator`，才能建立不可變 published 版本。

任一條件失敗時，命令必須零發布寫入並回傳錯誤。

## 稽核輸出

成功輸出與不可變 `published/<RuleSetId>/00000001.json` 均新增 `executionAudit`：

```json
{
  "executionAudit": {
    "approvedBy": "BCO\\kenny",
    "executedBy": "CodexAgent",
    "approvalReceiptReference": "rule-sets\\approval-drafts\\customer-patch-common-v1.approval.json",
    "requestChecksum": "<SHA-256>"
  }
}
```

`requestChecksum` 由 CLI 對實際讀取的 request 位元組計算，避免事後以同路徑替換 request 而失去可追溯性。`executionAudit` 與規則內容一同由 `RuleSetStore` 以 `FileMode.CreateNew` 寫入 published 檔；不得只輸出至主控台或另建可覆寫記錄。

## 對話確認邊界

Codex 只可在使用者提供下列明確確認後呼叫受控執行命令：

```text
確認發布 Customer-Patch 共用規則。
案件根目錄：<caseRoot>
核准人：<approvedBy>
```

Codex 必須先唯讀執行 preflight，並回報將使用的 request、`approvedBy` 與固定 `executedBy: CodexAgent`。若資訊不一致或 preflight 失敗，停止且不發布。

## 相容與淘汰

- 保留 `--publish-customer-patch-common-rule`，供既有人工執行相容使用；其 request 的 `actor` 語意維持不變，published 規則的 `executionAudit` 為空白。
- 新準備流程產生 `approvedBy` 格式 request，供 Codex 受控執行命令使用。
- `Publish-CustomerPatchRule.cmd`／`.ps1` 不再是發布必要條件，標示為舊版人工啟動器，不再由 Prompt 要求使用。
- 不修改既有已發布規則或歷程。

## 測試

至少涵蓋：

1. 相符 `approvedBy`、receipt 與 request 時，Codex 受控執行成功，published 規則持久保存固定 `CodexAgent` 與 request SHA-256。
2. receipt 操作者與 `approvedBy` 不一致時拒絕，且不建立 draft 或 published 規則。
3. request 企圖提供任意 `executedBy` 時拒絕。
4. 已存在 Customer-Patch 共用規則時拒絕重複發布。
5. CLI 命令與正式 Prompt 不再要求 PowerShell。

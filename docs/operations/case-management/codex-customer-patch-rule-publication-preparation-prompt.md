# Codex Prompt：Customer-Patch 規則核准與發布準備

本範本只準備 Customer-Patch Comparison 共用規則的人工核准草稿、待發布說明與正式發布 request。它固定使用 `rule-sets\approval-drafts` 與 `rule-sets\publication-requests`；不接受或產生舊的 `rule-sets\approvals` 路徑。它不建立或修改 `RuleSetDraft`、不發布規則、不產生版本號或 `ContentChecksum`，也不執行 Package 比較、preflight、Import、DB 或升級工具。

規則建立由受控 `RuleSetStore` 流程完成；具名人工操作員負責核准，Codex 僅能在後續取得明確發布確認後以固定 `CodexAgent` 執行。Codex 不得冒用人工身分或把待核准資料視為已發布規則。

## 最少輸入

```yaml
caseRoot: K:\\70.ArasUpgradeCases\\芯洲
actor: BCO\\kenny
humanApprovalDecision: 同意
```

`humanApprovalDecision` 只能是 `同意` 或 `不同意`，且必須是具名人工操作者當次明確提供的決定；未提供時一律視為 `待人工核准`。

## 系統固定帶入內容

下列欄位由範本固定帶入，不讓使用者手填：

```text
規則名稱：客戶基準對 Patch／Support 比較共同準則
規則類型：CustomerPatchComparison
規則範圍：Common
規則步驟：customer-patch-retain-items
規則語意：不自動刪除任何左、右側既有對象；僅保留 Item 供後續正式比較依規則產生報告與人工確認。
```

此共用規則不包含「右側既有對象刪除」規則。該規則須另有明確、經人工核准的判定條件與新版本，不能由本範本推測或自動加入。

當人工核准結論為「同意」時，發布 request 的下列欄位也必須由系統固定帶入，不讓使用者手填：

```text
ruleStoreRelativePath：rule-sets
approvalEvidenceRelativePath：rule-sets\approval-drafts\customer-patch-common-v1-approval-draft.md
approvalReceiptRelativePath：rule-sets\approval-drafts\customer-patch-common-v1.approval.json
approvedBy：具名人工操作者
```

## 可貼給 Codex 的 Prompt

```text
請依本 MD 準備 Customer-Patch Comparison 共用規則的人工核准與發布資料。

案件根目錄：<caseRoot>
具名人工操作者：<actor>
人工核准結論：<humanApprovalDecision>
執行動作：PREPARE_CUSTOMER_PATCH_RULE_PUBLICATION

正式 CLI 旗標為：

```text
--prepare-customer-patch-rule-publication <request.json>
```

此 command 只執行規則發布準備，不執行正式發布；成功後仍須由具名人工操作者明確確認，再使用 `codex-customer-patch-rule-publication-execution-prompt.md`。

先唯讀確認：

1. `<caseRoot>` 是明確且已建立案件根目錄，且案件清單可讀取；若 `<caseRoot>\rule-sets` 不存在，建立該規則儲存目錄。
2. 本次固定規則內容為 CustomerPatchComparison／Common／customer-patch-retain-items；不得加入其他 Rule 1、Rule 2、AML 或刪除步驟。
3. `humanApprovalDecision` 僅為具名人工操作員提供的結論；若缺少、不是「同意」或「不同意」，停止且不建立任何資料。

全部通過後，建立下列待人工核准／待發布資料，不得建立 RuleSetDraft 或 published 規則：

1. `<caseRoot>\rule-sets\approval-drafts\customer-patch-common-v1-approval-draft.md`
   - 記錄固定規則內容、具名人工操作者、人工核准結論與產生時間。
   - 明確標示「待受控發布；不是已發布規則」。
2. `<caseRoot>\rule-sets\publication-requests\customer-patch-common-v1.pending.md`
   - 記錄規則庫位置、固定規則內容與上述 approval draft 相對參考。
   - 明確標示「尚無 RuleSetId、版本號或 ContentChecksum」。
3. 僅當 `humanApprovalDecision` 為「同意」時，建立 `<caseRoot>\rule-sets\publication-requests\customer-patch-common-v1.request.json`。
   - 先建立 `<caseRoot>\rule-sets\approval-drafts\customer-patch-common-v1.approval.json`，固定寫入 `actor`、`decision: "Approved"`、`approvalEvidenceRelativePath` 與 `approvalEvidenceSha256`；兩者分別為 approval draft 的案件內相對路徑與 SHA-256。
   - 不得同時填入值不同的 `approvalDraftRelativePath`／`approvalEvidenceRelativePath` 或 `approvalDraftChecksum`／`approvalEvidenceSha256`；受控 CLI 對既有資料相容讀取兩種欄位名稱，但發現兩者不一致時必須停止。
   - 固定寫入 `caseRoot`、`ruleStoreRelativePath: "rule-sets"`、上述 approval draft／approval receipt 的相對路徑與具名 `approvedBy`。
   - 不得讓使用者填寫規則內容、規則類型、範圍、版本號、`ContentChecksum` 或 `executedBy`。
   - 明確標示為「待 Codex 取得明確發布確認後受控執行；不是已發布規則」。

若人工核准結論為「不同意」，approval draft 與 pending 資料必須標示為拒絕，不得建立 `customer-patch-common-v1.request.json`，也不得建議或要求發布。

### 既有發布準備資料的安全補齊

若 approval draft 與 `pending.md` 已存在、人工核准結論為「同意」，本動作必須先唯讀核對既有兩份資料中的固定規則內容、具名人工操作者與核准結論；全部一致時，安全補齊缺少的 approval receipt 與 request。

既有 `customer-patch-common-v1.request.json` 若缺少 `approvalReceiptRelativePath`、使用舊 `actor` 欄位、缺少 `approvedBy`，或其值不是本範本固定值，屬於舊版機器產生格式；不得要求使用者手動刪除。先驗證既有 request 的其餘固定欄位完全一致，將原檔移至同目錄 `customer-patch-common-v1.request.json.superseded-<UTC>.json` 保留，再寫入含 receipt 參考與 `approvedBy` 的新版 canonical request。此為唯一允許的受控 request 更新；不得覆寫 approval draft 或 `pending.md`。

若任一既有資料缺失、無法解析、核准結論不是「同意」，或內容與本範本固定內容不一致，停止並回報原因，不建立或修改任何檔案。既有 request 若有非固定欄位、其餘固定欄位不一致，或無法安全封存，也必須停止並回報，不得修正或覆寫。

完成後回報已建立或已驗證的資料完整路徑、人工核准結論，以及下列後續 Codex 受控動作：

具名人工核准人必須在新對話訊息明確輸入「確認發布 Customer-Patch 共用規則」及案件根目錄、核准人。Codex 先唯讀驗證 request、approval receipt 與 draft Checksum；通過後才以固定 `CodexAgent` 呼叫受控 CLI。發布成功後才會取得 RuleSetId、版本號、ContentChecksum 與不可變 `executionAudit`。

不得手工建立 `rule-sets\published\*.json`、不得計算或填入 ContentChecksum、不得宣告規則已發布，亦不得執行 Customer-Patch preflight 或 Package 比較。
```

## 受控發布後的下一步

只有受控 `RuleSetStore` 已發布 `CustomerPatchComparison` 共用規則，且可解析出版本與 `ContentChecksum` 後，才可使用 `PREFLIGHT_CUSTOMER_PATCH`。若規則庫只有本範本建立的 approval draft 或 pending 資料，preflight 必須維持 `Blocked`。

若 preflight 已因缺少已發布規則而 `Blocked`，修正順序固定為：完成本範本的人工核准資料準備；由具名核准人依 `codex-customer-patch-rule-publication-execution-prompt.md` 明確確認並執行受控發布；確認 `rule-sets\published\...` 已產生可解析的 `CustomerPatchComparison`／`Common` 規則與 `ContentChecksum` 後，再重新執行 `PREFLIGHT_CUSTOMER_PATCH`。不得重跑 preflight 取代發布，也不得手工建立或修改 published 規則檔。

## Codex 受控正式發布

在人工核准草稿已確認為「同意」後，本範本已在 `publication-requests\customer-patch-common-v1.request.json` 產生固定的發布 request。Codex 只能在核准人明確確認發布後執行；Codex 不能自行產生或變更核准。

自動產生的發布 request 範例：

```json
{
  "caseRoot": "K:\\70.ArasUpgradeCases\\芯洲",
  "ruleStoreRelativePath": "rule-sets",
  "approvalEvidenceRelativePath": "rule-sets\\approval-drafts\\customer-patch-common-v1-approval-draft.md",
  "approvalReceiptRelativePath": "rule-sets\\approval-drafts\\customer-patch-common-v1.approval.json",
  "approvedBy": "BCO\\kenny"
}
```

Codex 使用 `--execute-customer-patch-common-rule` 呼叫受控 CLI。它會拒絕 request 內任意 `executedBy`、缺少核准證據、規則庫外的核准證據、收據與草稿 Checksum 不一致，以及已有 Customer-Patch 共用規則的情況；成功後自動建立 draft、不可覆寫的 `published/<RuleSetId>/00000001.json`、`ContentChecksum` 與 `executionAudit`。`executionAudit` 固定保存 `approvedBy`、`executedBy: "CodexAgent"`、approval receipt 參考與 request SHA-256。

正式發布請使用 [codex-customer-patch-rule-publication-execution-prompt.md](codex-customer-patch-rule-publication-execution-prompt.md)；不需要 PowerShell 或交付啟動器。

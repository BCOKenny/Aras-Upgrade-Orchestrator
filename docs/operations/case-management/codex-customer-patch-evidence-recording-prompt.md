# Codex Prompt：登錄單一跳點 Customer-Patch 證據

本範本只執行單一跳點的受控 `Package.Cli --record-customer-patch-evidence`。它會建立該跳不可覆寫的 `patch-support-evidence.json` 與追加 history；不建立或發布 `rule-sets` 下的任何規則文件，也不執行 Package 比較、Rule 1、Rule 2、DB、SQL、Import、Aras Export 或升級工具。

## 參數範例

```yaml
caseRoot: K:\\70.ArasUpgradeCases\\芯洲
actor: BCO\\kenny
hopId: 11.0-sp8-to-11.0-sp15
```

`preparationId`、客戶基準與 request 路徑必須從唯一準備目錄的
`manifests\package-preparation-plan.template.json` 取得；不得從目錄名稱或版本字串猜測。

`comparison.source` 與 `comparison.target` 必須由該 plan 中與 `hopId` 相符的唯一比較解析；不使用 `sourceVersion` 或 `targetVersion`。

## 參數語意

- `comparison.source` 是唯一比較來源，必須使用客戶基準的 `role`、`baselineId`、`actualVersion` 及 `inputRelativePath`；`comparison.target` 是唯一比較目的，必須使用目標 Package 的 `targetRelease` 與 `inputRelativePath`。
- `patch-support-evidence.request.json` 是 CLI request，不是 plan：它必須使用頂層 `source` 與 `target`。頂層 `source` 只含 `baselineId`、`actualVersion`、`inputRelativePath`；頂層 `target` 只含 `targetRelease`、`inputRelativePath`。不得使用巢狀 `comparison`，也不得寫入 plan 專用的 `role`。
- request 的 `hopId` 必須等於唯一比較的 `comparisonId`；不得以 `comparisonId` 取代 CLI 的 `hopId` 欄位。
- 證據登錄不要求、驗證或保存 `sourceVersion`／`targetVersion`；兩側目錄僅由 `comparison.source.inputRelativePath` 與 `comparison.target.inputRelativePath` 決定。
- 例如客戶基準實際為 `11.0 SP9`、目標為 `R38` 時，plan 保留 `comparison.source.actualVersion: 11.0 SP9` 與 `comparison.target.targetRelease: R38`；不得以任一版本字串推導或阻擋比較。
- 此動作不判定 DB 升級相容性，只登錄已放入 `comparison.target.inputRelativePath` 的目標輸入證據。
- 因此人工輸入固定只有 `caseRoot`、`actor`、`hopId` 三項。

## 修復既有舊格式 request

此節只處理已存在的 `patch-support-evidence.request.json`。當檔案含有巢狀 `comparison`、使用 `comparisonId` 取代 `hopId`、或缺少頂層 `source.baselineId`、`source.actualVersion`、`source.inputRelativePath`、`target.targetRelease`、`target.inputRelativePath` 任一欄位時，判定為舊格式 request，必須停止證據登錄。

修復不是預設動作。使用者必須在 `CONFIRM_PATCH_INPUT` 的請求中明確提供：

```yaml
repairLegacyRequest: true
```

未提供此值時，只回報 `LegacyRequestRepairRequired` 與格式差異，不得修改 request。提供此值後，仍必須先完成下列零寫入預檢：

1. 由唯一 preparation 的 plan 解析與 `hopId` 相符的唯一比較。
2. 既有 request 的路徑、`caseRoot`、`preparationId` 與 actor 與本次指定案件相符。
3. 該跳尚不存在 `patch-support-evidence.json`。
4. 由 plan 可完整建立 CLI request 的 `hopId`、頂層 `source`、頂層 `target`、`evidenceRelativePath`、`patchSupportSource` 與 actor；不得猜測任何值。
5. 修復用備份檔路徑尚不存在。

全部通過後，先建立不可覆寫備份，再覆寫原 request：

1. 在同一個 `evidence` 目錄，以 `patch-support-evidence.request.legacy-<UTC 時間戳>.json` 建立備份；不得覆寫既有備份。
2. 驗證備份內容與原 request 的位元組及 SHA-256 相同。
3. 僅以 plan 資料建立新的 CLI request：`caseRoot`、`preparationId`、`hopId`、頂層 `source`、頂層 `target`、`evidenceRelativePath`、`patchSupportSource`、`actor`；不得使用巢狀 `comparison` 或 `comparisonId`。
4. 覆寫後重新讀取 request，確認其欄位、案件內相對路徑及 `patchSupportSource == target.inputRelativePath` 均正確。

修復完成即停止：不得執行 `--record-customer-patch-evidence`，不得建立 `patch-support-evidence.json`，不得追加 history，也不得修改 Package、plan、案件檔或其他跳點。使用者必須在另一個明確的證據登錄動作中重新執行本文件。

## 可貼給 Codex 的 Prompt

```text
請依本 MD 的參數執行單一跳點 Customer-Patch 證據登錄。

案件根目錄：<caseRoot>
登錄者：<actor>
工作流：CUSTOMER_PATCH_COMPARISON
執行動作：RECORD_CUSTOMER_PATCH_EVIDENCE
跳點識別：<hopId>

若本次只要修復既有舊格式 request，另明確加入：

repairLegacyRequest: true

執行前先唯讀確認：

1. `<caseRoot>\package-upgrade` 下只有一個可用準備目錄；若多個候選或沒有候選，停止並要求明確指定。
2. 從 `package-preparation-plan.template.json` 解析與上述 hopId 完全相同的唯一比較，取得其 `comparison.source` 與 `comparison.target`；workflow 必須為 `CUSTOMER_PATCH_COMPARISON`。找不到或有多筆相同 hopId 時停止。
3. 該跳 `customer-patch-comparison\patch-support-input` 存在且包含檔案。
4. 該跳 `evidence\patch-support-evidence.request.json` 存在。
5. request 的 caseRoot、preparationId、hopId、頂層 `source` 與頂層 `target` 必須與本次參數及第 2 項解析的比較資料完全一致；`hopId` 必須等於該比較的 `comparisonId`，且 request 不得有巢狀 `comparison`。
6. request 的 actor 等於 <actor>。
7. request 的 patchSupportSource 與頂層 `target.inputRelativePath` 完全相同，且兩者皆為安全的案件內相對路徑。
8. 該跳 evidence 目錄尚不存在 `patch-support-evidence.json`。
9. `<caseRoot>\aras-upgrade-case.json` 存在且可讀。

任一檢查失敗時停止，不修改任何檔案。

若偵測到舊格式 request，且使用者已明確提供 `repairLegacyRequest: true`，改依「修復既有舊格式 request」節執行；該次只修復 request，結束後不得接續執行 evidence 登錄。

全部通過後，僅執行：

Package.Cli --record-customer-patch-evidence <該跳 patch-support-evidence.request.json>

完成後回報：

- `patch-support-evidence.json` 完整路徑。
- 輸入 tree checksum。
- `verifiedBy` 與 `verifiedAt`。
- history 追加結果。

不得執行 Package 比較、preflight、Rule 1、Rule 2、DB、SQL、Import、Aras Export 或升級工具。
```

## 下一步

證據登錄成功後，使用 `codex-package-comparison-preparation-prompt.md` 的 `PREFLIGHT_CUSTOMER_PATCH` 執行「已發布 Customer-Patch 規則解析＋唯讀 preflight」合併關卡。實際 Customer-Patch 比較 command 尚未公開，不得以手動複製或舊 Rule 1 CLI 取代。

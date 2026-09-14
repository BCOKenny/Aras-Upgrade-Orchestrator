# 客戶基準對 Patch／Support 比較設計

## 目的

新增獨立的「客戶基準對 Patch／Support 比較」工作流，讓每一個 Package Import 跳點都能以同一份不可變客戶 Package 基準，比較該跳的目標 Patch／Support Package XML 樹，產生可追溯的比較報告與候選 Patch Package 副本。

本工作流不取代、不修改或重命名既有的 OOTB Rule 1 工作流。既有 OOTB Rule 1 仍只比較兩端 OOTB `Solutions`；直接 Rule 2 不以其輸出作為輸入或前置條件。新工作流不得將其輸出偽裝成 OOTB `SourceDiff`、`TargetDiff` 或 Rule 1 差異包。

## 範圍與非目標

### 包含

- 共用客戶 Package 基準與各跳 Patch／Support 的唯讀比較。
- 比較開始前的輸入、路徑、版本、規則與輸出隔離驗證。
- XML 檔案位元相等判定、AML 語意比較、四種 Item 結果與人工確認。
- 新 attempt 中的客戶比較副本、候選 Patch 副本、比較報告與驗證結果。
- 受控 CLI、案件歷程、目錄鎖與不可覆寫 attempt。

### 不包含

- 修改 `customer-package-baseline/.../input`、`patch-support-input`、正式升級工具或其 `Support\\Solutions`。
- 啟動 Patch、Package Import、Aras Upgrade Tool、Aras Export、SQL 或 DB 升級。
- 自動將候選 Patch 副本寫回實際升級工具。
- 改變既有 OOTB Rule 1／Rule 2 command、CLI request、完成收據或封裝格式。
- 比較、解析或修改非 XML 檔案。

## 目錄模型

新的比較工作流使用下列目錄；客戶基準不得複製到每一跳：

```text
package-upgrade/
  preparation-<preparation-id>/
    customer-package-baseline/
      <customer-baseline-id>/
        input/                                      # 共用 source、不可變
        evidence/
    hops/
      <hop-slug>/
        customer-patch-comparison/
          patch-support-input/                      # target、不可變
          evidence/
          comparison-attempts/
            <attempt-id>/                           # 每次必須是新目錄
              validation-result.json
              CustomerComparisonCopy/
              PatchCandidateCopy/
              comparison-report.json
          candidate-package-imports/                # 僅放已核定的候選輸出
```

`patch-support-input` 就是目標比較根目錄，不使用 `Support\\Solutions` 子路徑。操作人員必須將該跳預計使用的完整 `Solutions` Package 樹之內容放在此根目錄；不得將整個 `Support` 目錄、Log、commands、backup、tools 或不相關 XML 一併放入。

### source／target 輸入與跳點版本語意

Customer-Patch 比較的 canonical 資料模型如下。只有 `comparison.source` 與 `comparison.target` 是比較器可讀取的實體輸入；不使用跳點 `sourceVersion`／`targetVersion`。

```yaml
comparison:
  source:
    role: CustomerBaseline
    baselineId: customer-11sp9
    actualVersion: 11.0 SP9
    inputRelativePath: package-upgrade/preparation-<preparation-id>/customer-package-baseline/customer-11sp9/input

  target:
    role: TargetPackage
    targetRelease: R38
    inputRelativePath: package-upgrade/preparation-<preparation-id>/hops/11.0-sp8-to-11.0-sp15/customer-patch-comparison/patch-support-input

```

`comparison.source` 永遠是唯一的客戶 Package 基準；`comparison.target` 永遠是目標版本（例如 R38）的 Package 樹。兩者的 `inputRelativePath` 決定比較目錄；`source`／`target` 只表示本次比較兩側，不表示升級跳點版本。CLI 相對路徑必須以案件根目錄為基準，因此必須包含 `package-upgrade/preparation-<preparation-id>/`；不得從 preparation 目錄內再重複解析相對路徑。plan 的 hop 可保留 `sourceVersion`／`targetVersion`，但它們僅為跳點中繼資料，不能推導或覆蓋 `comparison.source`／`comparison.target`。

每一比較的 `evidence/` 必須由受控登錄產生 `patch-support-evidence.json`。plan 保留 `comparison.source` 與 `comparison.target`；`patch-support-evidence.request.json` 則是 CLI request，必須使用頂層 `source` 與 `target`，不得使用巢狀 `comparison`。其 `hopId` 等於 plan 的 `comparisonId`，`patchSupportSource` 固定等於同一 request 頂層 `target.inputRelativePath`。`evidenceRelativePath` 只指向 evidence 目錄，不得包含 `patch-support-evidence.request.json`。證據固定 `source`、`target`、輸入 tree checksum、具名 `verifiedBy` 及受控 CLI 寫入的 `verifiedAt`；不包含或驗證 `sourceVersion`／`targetVersion`。

既有 request 若為巢狀 `comparison`、使用 `comparisonId` 或缺少 CLI 頂層欄位，屬於 `LegacyRequestRepairRequired`，不可直接登錄 evidence。只有具名使用者明確提供 `repairLegacyRequest: true` 時，系統才可從同一跳 plan 重建 request；必須先建立不可覆寫備份並驗證其 SHA-256，修復後重新讀取驗證。修復與 evidence 登錄是兩個動作：修復不得執行 `--record-customer-patch-evidence`、不得建立 evidence 或追加 history。

既有 OOTB 流程如需保留，應使用獨立的 `ootb-rule1/` 子目錄與 `ootb-source-solutions`、`ootb-target-solutions`、`attempts`，不得與新工作流共用輸出或候選產物。既有舊骨架不得由新 Prompt 自動搬移、刪除或改名。

## 輸入與前置驗證

### Scaffold、evidence 與規則發布的階段界線

正式 `--scaffold-customer-patch-preparation` 只建立 preparation 目錄、非空 plan、每跳
`patch-support-evidence.request.json` 草稿與空目錄；它不建立或發布 `rule-sets`，也不產生正式
`patch-support-evidence.json`。request 草稿不能當成 evidence，空的 `rule-sets` 也不能當成已發布規則。

執行 `PREFLIGHT_CUSTOMER_PATCH` 前，每一跳都必須先完成受控 evidence 登錄，並且案件層級
`rule-sets\published\...` 必須存在可解析、類型為 `CustomerPatchComparison` 的已發布規則與固定
`ContentChecksum`。規則發布準備資料（approval draft、pending 說明、publication request 或 approval receipt）
只代表待核准／待發布狀態，不得交給 preflight 當成有效規則。任一正式 evidence 或已發布規則缺少時，
preflight 必須回報 `Blocked`，不得自動補建、猜測或調整 Rule 2 內容。

`ruleStoreRoot` 的存在不代表 Policy 已就緒。它只提供 published rule 的搜尋範圍；approval draft、pending 說明、publication request、approval receipt 或空目錄都不是可供 preflight 使用的規則。正式規則必須能被解析為 `CustomerPatchComparison`，且帶有可驗證的固定 `ContentChecksum`，並與本次基準識別及目標版本唯一相容。

目前 CLI 以 `policy.invalid` 作為上述 Policy 驗證失敗的總括阻擋碼，適用於規則根目錄缺失／不可讀、沒有 published rule、格式或類型不符、Checksum 缺失／不符，以及規則候選無法唯一解析等情況。回報時應保留可辨識的細節與 `policyChecksum: null`（若尚未解析成功）；不得將這些情況混寫成單一的「rule-sets 路徑不存在」，也不得將目錄存在誤判為規則已發布。

缺少已發布規則時的修正路徑固定如下，不得以重跑 preflight 或手工建立
`rule-sets\published\*.json` 取代：

1. 依 `codex-customer-patch-rule-publication-preparation-prompt.md` 建立或驗證人工核准 draft、pending 說明、approval receipt 與 publication request。
2. 由具名核准人明確確認發布；Codex 先以目前原始碼建置的 Package CLI 執行發布 preflight。
3. 發布 preflight 通過後，才由受控 `RuleSetStore` 建立不可覆寫的 `CustomerPatchComparison`／`Common` published 規則與 `ContentChecksum`。
4. 重新執行本工作流的 `PREFLIGHT_CUSTOMER_PATCH`。只有能解析該 published 規則、版本與 `ContentChecksum` 時，才可判定規則關卡通過。

若人工核准尚未完成、approval receipt 缺失或 Checksum 不一致，仍維持 `Blocked`；不得以 approval draft、pending 說明、publication request 或 receipt 宣告規則已發布。

每個跳點 plan 與 request 必須明確提供：

- plan 的 `comparison.source.role`、`comparison.source.baselineId`、明確輸入的 `comparison.source.actualVersion` 與案件根目錄相對的 `comparison.source.inputRelativePath`。
- plan 的 `comparison.target.role`、`comparison.target.targetRelease` 與案件根目錄相對的 `comparison.target.inputRelativePath`。
- CLI request 的頂層 `source`、頂層 `target`、`hopId` 與只指向 evidence 目錄的 `evidenceRelativePath`。
- `comparisonAttemptsRelativePath` 與新的 `attemptId`。
- 具名 `actor`。
- 已發布的 Customer-Patch Comparison Policy 版本與有效 Checksum。
- 受控動作 safety whitelist，必要時提供單次 confirmation。

比較前先執行唯讀 preflight。下列任一條件不滿足時，結果必須為 `Blocked`，只回傳驗證結果與原因，不建立 attempt 或候選 Package：

1. 任一必填欄位空白、路徑為絕對路徑、含 `..` 或逸出案件根目錄。
2. 客戶基準或 Patch／Support 根目錄不存在、沒有 XML，或輸出／候選目錄已存在或與輸入重疊。
3. Patch／Support 輸入不符合「單一跳目標 Package XML 樹」結構，或其版本／跳點證據未提供。
4. Policy 未發布、無法解析、版本衝突或 Checksum 無法固定。
5. 安全白名單、必要前置條件或確認快照不相符。

`Blocked` 不是修正動作。工具只回報缺少、衝突或不符合規格的項目；操作人員補正輸入或規格後，必須使用新的 `attemptId` 重跑。

前置驗證狀態必須區分「執行狀態」與「驗證結果」：已呼叫 preflight command 並取得 `Ready` 或 `Blocked` 結果，即表示前置驗證已執行；`Blocked` 表示已執行但未通過，不得回報為尚未執行。只有尚未呼叫 command 且沒有結果時，才是尚未執行。`Blocked` 時不得建立 comparison attempt 或進入實際 Package 比較。

### 受控 Patch／Support 證據登錄

同一位操作人員可在確認目標 Package 樹後，執行 `--record-customer-patch-evidence <request.json>`。此 command 必須重新計算 `patch-support-input` 的 tree checksum，使用 request 的具名 `actor` 寫入 `verifiedBy`，以 command 執行時間寫入 `verifiedAt`，並追加 history。已存在的證據不得覆寫；Patch／Support 內容變更後，既有證據 checksum 不符，preflight 必須 `Blocked`。此 command 不比較 AML、不建立 comparison attempt，且不修改任何 Package 輸入。

## 比較與輸出規則

原始客戶基準與 Patch／Support 一律唯讀。所有刪除、保留與候選輸出只發生在新 attempt 的工作副本。

### 檔案層級

1. 僅依相同相對路徑配對 `.xml`；不得跨目錄配對同名檔案。
2. 非 XML 檔案不讀取、不解析、不比較、不複製到候選輸出；只在輸入清單中標示為不適用。
3. 若同一相對路徑的兩個 XML 檔案位元內容完全相同（以 SHA-256 相同確認），`PatchCandidateCopy` 不建立該 XML；客戶原始輸入與 `CustomerComparisonCopy` 仍保留該 XML。
4. 只要 XML 檔案位元內容不同，即建立兩側可用的比較副本並進行 AML 語意比較；即使 AML 語意相同，也不得因語意相同而刪除 Patch 候選 XML。

### Item 層級

對檔案位元不同的 XML，四種結果決定本工作流是否建立候選處理內容；刪除處理仍必須等候獨立的版本化刪除規則：

| 結果 | `CustomerComparisonCopy` | `PatchCandidateCopy` | 報告與處理 |
|---|---|---|---|
| 左側（客戶）獨有 | 保留供報告 | 不建立 | 視為客戶新增，不修改、不刪除。 |
| 右側（Patch）獨有 | 不適用 | 不建立 | 視為目標版新增，保留右側給未來 Import 自行新增。 |
| 兩端相同 Item | 保留供報告 | 不建立 | 不需處理。 |
| 兩端不同 Item | 保留 | 依已發布差異規則建立 | 僅處理已存在於兩側的系統對象差異；無規則時轉人工確認。 |

任何一側獨有 Item 都不得自動刪除或加入候選 Patch。右側既有對象的刪除規則尚待另行定義；在該規則發布前，任何可能刪除的情況都必須轉人工確認。

### AML 語意比較

針對位元不同的 XML，必須遵守 `docs/standards/AML_Structure_and_Traversal_Standard.md`：

1. 安全解析 AML，拒絕 DTD 與外部 Entity。
2. 比較 AML Root 名稱、Namespace 與 attributes。
3. Item 與 Relationship Item 使用 `type + id + action` 配對；無 `id` 時使用 `type + canonicalized(where) + action`。
4. Scalar Property 比較名稱、attributes 與文字值。
5. Item Property 依名稱配對，並遞迴比較內含 Item。
6. `Relationships` 不依出現順序配對；Relationship Item 也依 CompareKey 配對。
7. 缺少或重複 CompareKey、缺少 `type`／`action`／可用 `id` 或 `where`、重複同名 Property、無法建立可靠結構時，建立人工確認項目。
8. XML 宣告、縮排與格式不影響 AML 語意比較；它們仍會使檔案位元判定為不同，因此候選 Patch XML 必須保留。

## 狀態、候選與核定

| 狀態 | 條件 | 可用性 |
|---|---|---|
| `Blocked` | preflight、AML 解析或安全條件失敗。 | 沒有候選 Package。 |
| `PendingReview` | 比較完成，但有人工確認項目。 | 僅供檢視，不可作為 Package Import。 |
| `PendingApproval` | 比較完成且無錯誤、無人工確認。 | 候選副本存在，仍不可直接寫入正式 `Support\\Solutions`。 |
| `Approved` | 操作人員對相同 attempt、輸入摘要與 Policy Checksum 完成核定。 | 可交接至後續受控 Package Import 準備程序。 |

核定前不得將 `PatchCandidateCopy` 寫入正式升級工具。若未來要自動處理 Item 層級內容，必須新增已發布的版本化 Policy；未定義的處理一律保持人工確認，AI 不得推測。

## CLI 與文件調整範圍

新增而非改寫既有 Rule 1 CLI：

- `--scaffold-customer-patch-preparation <request.json>`：以 UTF-8 request 在既有 `caseRoot\package-upgrade` 下建立新的 preparation。它先完成零寫入預檢與記憶體 JSON round-trip，於 staging 建立非空 plan 與每跳 evidence request，逐檔重新解析後以單次目錄 move 發布。它不建立 `rule-sets`、不發布規則、不寫 evidence、history 或案件清單，也不讀取任何 Package 輸入。
- `--record-customer-patch-evidence <request.json>`：記錄同一位人工操作人員已確認的跳點、來源與不可變輸入摘要。
- `--preflight-customer-patch-comparison <request.json>`：解析已發布 Customer-Patch Policy 與固定 Checksum 後執行唯讀驗證，無輸出 attempt、無 history、無鎖。
- `--compare-customer-patch <request.json>`：建立新 attempt、兩側副本、報告與不可覆寫 history。
- `--approve-customer-patch <request.json>`：僅核定既有 `PendingApproval` attempt，固定輸入摘要與 Policy Checksum；不修改正式升級工具。
- `--publish-customer-patch-common-rule <request.json>`：僅供具名人工操作員以已核准證據發布固定的 `CustomerPatchComparison`／`Common`／`customer-patch-retain-items` 共用規則；成功後由 `RuleSetStore` 建立不可覆寫的 published 版本與 `ContentChecksum`。

Customer-Patch 規則發布準備 Prompt 在人工核准結論為「同意」時，必須同次建立 approval draft、pending 說明與固定欄位的 `publication-requests/customer-patch-common-v1.request.json`。該 request 只包含案件根目錄、`rule-sets`、approval draft 相對路徑與具名 actor；不得容許使用者填寫規則內容、範圍、版本或 Checksum。核准結論不是「同意」時不得產生 request。若既有 approval draft 與 pending 說明已通過固定內容核對但 request 缺少，重跑發布準備只可補建該 request，且不得覆寫任何既有資料；內容不一致時必須停止。準備 request 不等同發布，只有具名人工操作員執行正式 CLI 才可建立 published 規則。

核准結論為「同意」時，發布準備也必須建立 `approval-drafts/customer-patch-common-v1.approval.json`。該收據固定記錄 actor、`Approved` 決定、approval draft 相對路徑與 SHA-256。發布 request 必須引用收據相對路徑；正式發布與啟動器均須驗證 actor、決定及 draft Checksum。既有 request 缺少收據時不可發布，只能由安全補齊流程建立缺少的收據與新 request，且不得覆寫既有證據。

正式發布執行 Prompt 只接受案件根目錄、具名 actor 與發布 request 相對路徑，唯讀確認 request、approval draft 與既有 published 狀態後，輸出唯一 CLI 命令交由該人工操作員執行。Prompt 不得代替人工操作員呼叫 CLI 或建立規則。

正式發布採「具名人工核准、Codex 受控執行」：approval receipt 固定核准人與 draft SHA-256，Codex 僅在明確發布確認後以 `--execute-customer-patch-common-rule` 執行，並在不可變 published 規則保存 `executionAudit`。PowerShell 啟動器僅為舊版相容工具，不得作為必要步驟或繞過 `RuleSetStore`。詳細設計見 `docs/superpowers/specs/2026-09-03-customer-patch-codex-execution-audit-boundary-design.md`。

目錄建立 Prompt 必須使用 `--scaffold-customer-patch-preparation` 建立本文件的目錄模型與 request 草稿；不得為這個新工作流產生 `ootb-source-solutions`、`ootb-target-solutions`、`rule1-attempts` 或 `rule2-attempts`，也不得以暫存 PowerShell 腳本產生 plan。

## 驗收案例

1. 共用客戶基準可被多個互不重疊跳點讀取，且不被修改。
2. `--record-customer-patch-evidence` 必須以具名 actor、實際執行時間及輸入 tree checksum 產生不可覆寫證據；不能修改輸入。
3. Patch／Support 根目錄缺失、沒有 XML、版本證據缺失或 checksum 已異動、路徑逸出、輸出重疊、Policy 未發布或 Checksum 不符時，preflight 必須 `Blocked` 且零寫入。
4. 非 XML 檔案不比較、不解析、不輸出到候選副本。
5. 同相對路徑 XML 位元完全相同時，只省略 `PatchCandidateCopy` 的該 XML；客戶輸入與客戶比較副本不變。
6. XML 位元不同但 AML 語意相同時，兩側副本保留，報告標示語意相同。
7. 來源獨有、目標獨有、相同與不同 Item 均保留於適用副本，並具正確統計與 AML Path。
8. AML 解析錯誤或任何人工確認時，候選結果不得 `Approved`。
9. 已有 attempt、重用 attempt ID、輸入異動或核定快照不符時拒絕覆寫或核定。
10. 既有 OOTB Rule 1／Rule 2 CLI、收據、封裝與測試不受影響。
11. 規則發布準備在核准為「同意」時，同次產生固定欄位的發布 request；核准為「不同意」時不得產生 request 或 published 規則。
12. 已存在且內容一致的 approval draft 與 pending 說明缺少 request 時，重跑發布準備只補建 request；任何既有資料不一致或既有 request 不一致時，必須停止且零覆寫。

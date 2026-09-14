# Codex Prompt：Customer-Patch 輸入確認與 OOTB Rule 1 分流

完整的 Customer-Patch 1～7 項程序、對應文件與可貼 Prompt 範例，請參閱 [`customer-patch-procedure-map.md`](customer-patch-procedure-map.md)。

本文件包含兩個互斥分支；必須先從已建立準備目錄的
`package-preparation-plan.template.json` 選定工作流，才可顯示或執行該分支的動作。若範本不存在、工作流缺少或值不明，必須停止，不得預設為 `OOTB_RULE1`，也不得列出 Rule 1 作為 Customer-Patch 的下一步：

1. 確認客戶 Package 基準與各跳點 Patch／Support 輸入，並將每跳
   `comparison.target.inputRelativePath` 自動複製至對應 request 的 `patchSupportSource`。
2. 產生 OOTB Rule 1 比較的人工登錄、準備與確認草稿。

工作流選擇：

```text
CUSTOMER_PATCH_COMPARISON：客戶基準對各跳點 Patch／Support 的比較。
OOTB_RULE1：來源版 OOTB Solutions 對目標版 OOTB Solutions 的 Rule 1 比較。
```

## Customer-Patch 資料模型

`CUSTOMER_PATCH_COMPARISON` 必須使用下列語意模型：

```yaml
comparison:
  source:
    role: CustomerBaseline
    baselineId: <customerPackageBaselineId>
    actualVersion: <customerBaselineActualVersion>
    inputRelativePath: customer-package-baseline/<customerPackageBaselineId>/input

  target:
    role: TargetPackage
    targetRelease: R38
    inputRelativePath: hops/<comparisonId>/customer-patch-comparison/patch-support-input
```

只有 `comparison.source` 與 `comparison.target` 是比較輸入；不使用 `sourceVersion`、`targetVersion` 或 `packageHopContext`。

## Customer-Patch 可用動作

`CUSTOMER_PATCH_COMPARISON` 的可用動作僅為 `CONFIRM_PATCH_INPUT` 與 `PREFLIGHT_CUSTOMER_PATCH`。選定此工作流時，不得顯示、建議或執行 `GENERATE_OOTB_RULE1_DRAFT`。

```text
CONFIRM_PATCH_INPUT：確認既有輸入並更新 patch-support-evidence.request.json。
PREFLIGHT_CUSTOMER_PATCH：解析已發布 Customer-Patch 規則／Checksum，並執行唯讀 preflight。
```

選擇 `CUSTOMER_PATCH_COMPARISON` 時，必須執行本文件的客戶 Patch／Support 輸入確認，並在預檢通過後實際更新三份 `patch-support-evidence.request.json`；不得要求 OOTB `Solutions` 路徑、Rule 1 attempts、Rule 1 規則庫或 Rule 1 登錄草稿。固定路徑由準備目錄與 `hopId` 自動組成。

Customer-Patch 的完整程序順序固定為：

```text
1. codex-package-preparation-prompt.md
   → 建立 preparation、plan 與三份 evidence request 草稿
2. 人工放入三跳的客戶基準與 Patch／Support Package 檔案
3. codex-customer-patch-rule-publication-preparation-prompt.md
   → 建立規則核准 draft、receipt 與 publication request
4. 人工明確確認發布
   → 確認發布 Customer-Patch 共用規則；核准人必須為具名人工操作者
5. codex-customer-patch-rule-publication-execution-prompt.md
   → 建立 rule-sets\published\<RuleSetId>\00000001.json
6. codex-customer-patch-evidence-recording-prompt.md
   → 逐跳建立正式 patch-support-evidence.json 與 history
7. 本文件的 PREFLIGHT_CUSTOMER_PATCH
   → 解析正式 evidence 與 published rule，回報 Ready 或 Blocked
8. 實際 Customer-Patch Package 比較
   → 目前尚無正式比較 CLI；只有在正式 command 發布後，且第 7 項為 Ready，才可開始
```

第 6 項只負責正式 evidence；重新執行 evidence 登錄不會建立或補建
`rule-sets\approval-drafts`、`rule-sets\publication-requests`、`rule-sets\drafts` 或
`rule-sets\published`。第 3～5 項才負責規則相關目錄與文件；不得以第 6 項替代規則發布。

不得根據舊的 Rule 1 preparation、舊 Rule 1 草稿、其他 `preparation-*` 目錄或先前對話內容，判定目前 Customer-Patch 已完成、未完成或應改走 Rule 1；只能依目前指定 preparation 的 plan、其 Customer-Patch evidence 與 preflight 結果判定。

## OOTB Rule 1 獨立分支

只有使用者明確選擇 `OOTB_RULE1`，且 plan 的 `workflow` 同為 `OOTB_RULE1` 時，才可使用 `GENERATE_OOTB_RULE1_DRAFT` 與後續 Rule 1 登錄段落。缺少 OOTB 路徑時必須停止，不得猜測；此分支不會改變任何 Customer-Patch 的輸入、evidence 或完成狀態。

工作流與動作必須符合下列配對；其他組合一律停止：

```text
CUSTOMER_PATCH_COMPARISON + CONFIRM_PATCH_INPUT
CUSTOMER_PATCH_COMPARISON + PREFLIGHT_CUSTOMER_PATCH
OOTB_RULE1 + GENERATE_OOTB_RULE1_DRAFT
```

兩類作業可分開執行。`patchSupportSource` 更新只修改各跳點的
`patch-support-evidence.request.json`；Rule 1 草稿只寫入指定 `manifests`，不得混淆兩種工作流。

## 客戶 Patch／Support 輸入確認

在人員已將檔案放入對應目錄後，先以本段 Prompt 確認三個跳點。plan 使用 `comparison.source`／`comparison.target`；但 `patch-support-evidence.request.json` 是 CLI request，必須使用頂層 `source` 與 `target`。Codex 會在所有唯讀預檢通過後，將每跳 plan 的兩側資料複製到 request 頂層 `source`／`target`，並將已驗證的 `target.inputRelativePath` 寫入同一份 request 的 `patchSupportSource`，不需要人工填寫來源文字。不得使用巢狀 `comparison`。

參數範例：

```yaml
caseRoot: K:\\70.ArasUpgradeCases\\芯洲
actor: KENNYCHANG6024\\kenny
workflow: CUSTOMER_PATCH_COMPARISON
operation: CONFIRM_PATCH_INPUT
```

`preparationId`、`customerPackageBaselineId` 與完整跳點不可由 Codex 從資料夾名稱猜測；Codex 必須先讀取案件內的
`package-upgrade\<preparationId>\manifests\package-preparation-plan.template.json`，並使用其中明確記錄的值。

## 最少輸入模式

若案件已依 `codex-package-preparation-prompt.md` 建立準備目錄，且使用者提供
Customer-Patch Comparison 的已發布規則儲存位置，後續只需提供以下三項資料：

```text
案件根目錄：K:\70.ArasUpgradeCases\芯洲
登錄者：BCO\kenny
Customer-Patch Comparison 已發布規則儲存位置：K:\70.ArasUpgradeCases\芯洲\rule-sets
```

上述三項資料的最少輸入模式固定對應：

```text
workflow: CUSTOMER_PATCH_COMPARISON
operation: PREFLIGHT_CUSTOMER_PATCH
```

不得再要求使用者輸入 `preparationId`、`comparisonId`、來源／目標版本、
`inputRelativePath`、`attemptId` 或其他可由 plan 與案件目錄安全取得的欄位。
若使用者要執行 `CONFIRM_PATCH_INPUT`，必須明確指定該動作；不得從三項最少輸入
自行改走確認分支。

Codex 必須依序執行：

1. 在 `<CASE_ROOT>\package-upgrade` 尋找準備目錄；若有多個候選或無法唯一確認，停止並要求指定，不得依日期猜測。
2. 讀取唯一準備目錄的 `manifests\package-preparation-plan.template.json`。
3. 從範本取得 `preparationId`、`comparisonId`、`comparison.source`、`comparison.target`、工作流及所有相對路徑。
4. 在三項最少輸入模式下，將執行動作固定為 `PREFLIGHT_CUSTOMER_PATCH`；若使用者明確指定其他動作，則依明確指定值驗證允許配對，不得自行改走另一個分支。
5. `CUSTOMER_PATCH_COMPARISON + CONFIRM_PATCH_INPUT`：只執行客戶 Patch／Support 確認與 request 更新，不要求 Rule 1 規則庫或 OOTB 路徑，也不建立 Rule 1 登錄草稿。
6. `CUSTOMER_PATCH_COMPARISON + PREFLIGHT_CUSTOMER_PATCH`：確認既有 Customer-Patch evidence、解析已發布 Customer-Patch Comparison 規則與 Checksum，並執行唯讀 preflight；要求 Customer-Patch Comparison 已發布規則儲存位置，不建立 Rule 1 登錄草稿或要求 OOTB 路徑。
7. `OOTB_RULE1 + GENERATE_OOTB_RULE1_DRAFT`：再要求並驗證 `Rule 1 已發布規則儲存位置`；依讀取到的跳點與骨架產生 Rule 1 登錄草稿。缺少 OOTB `Solutions` 或其他必要資訊時停止，不猜測。

最少輸入模式會由每跳 `patch-support-input` 的案件內相對路徑自動產生 `patchSupportSource`。若任一輸入目錄不存在、沒有檔案或 request 路徑不相符，停止且不得修改 request。

若 `CONFIRM_PATCH_INPUT` 偵測既有 request 含巢狀 `comparison`、`comparisonId` 或缺少 CLI 頂層欄位，必須回報 `LegacyRequestRepairRequired` 並停止。只有使用者明確提供 `repairLegacyRequest: true` 時，才可依 `codex-customer-patch-evidence-recording-prompt.md` 的修復流程先建立不可覆寫備份並重建 request；修復動作不得執行 `--record-customer-patch-evidence`、不得建立 `patch-support-evidence.json` 或追加 history。

可貼給 Codex 的確認 Prompt（此分支會在預檢通過後實際更新外部 request）：

```text
請依本文件參數執行「Patch／Support 輸入確認與 request 來源說明更新」。

本次 workflow 必須是 CUSTOMER_PATCH_COMPARISON。不要要求 OOTB Solutions 路徑、Rule 1 規則庫或 Rule 1 登錄資料。
本次執行動作必須是 CONFIRM_PATCH_INPUT。不得執行或產生 preflight、Rule 1 草稿或任何比較。

先唯讀確認：
1. `comparison.source.inputRelativePath` 指向的 customer-package-baseline\\<customerPackageBaselineId>\\input 存在且包含檔案；它是所有目標比較共用的來源客戶基準。
2. 各比較的 `comparison.target.inputRelativePath` 指向的 customer-patch-comparison\\patch-support-input 存在且包含檔案；它是目標 Package 輸入。
3. 每跳 evidence\\patch-support-evidence.request.json 存在。
4. 每份 request 的 caseRoot、preparationId、hopId、頂層 `source`、頂層 `target`、相對路徑與 actor 正確；`hopId` 必須等於該比較的 `comparisonId`，且 request 不得使用巢狀 `comparison`。
5. 每份 request 的 patchSupportSource 目前為 <待人工填寫>，或已等於該比較複製到 request 頂層的 `target.inputRelativePath`。
6. 每一比較的 `comparison.target.inputRelativePath` 是案件內相對路徑、不含 `..`，且解析後正好對應已確認的 patch-support-input 目錄。

任一檢查失敗時停止，不修改任何 request，也不得改產生 Rule 1 草稿。
全部通過後，必須將每個 `comparison.target` 複製為對應 request 的頂層 `target`，並將其 `inputRelativePath` 寫入同一 request 的 patchSupportSource 欄位；頂層 `source` 亦必須複製自同一比較的 `comparison.source`，
不得修改其他欄位、Package、案件檔或歷程。

不要執行 --record-customer-patch-evidence、Package 比較、Rule 1、Rule 2、Import、DB、SQL、
Aras Export 或任何升級工具。
完成後回報三個跳點的輸入檔案數、三份更新路徑及每跳自動帶入的案件內相對路徑。
```

更新後仍須由具名操作人員逐跳執行正式的
`Package.Cli --record-customer-patch-evidence <request.json>`；直接更新來源文字不等於正式證據已建立。

## Customer-Patch 規則驗證與 preflight

此節合併原本的「已發布比較規則驗證」與「唯讀 preflight」。它只能在每一跳已存在
`evidence\patch-support-evidence.json`，且案件層級 `rule-sets` 已有可解析的已發布
`CustomerPatchComparison` 規則後執行；不會登錄 evidence、不會修改任何輸入，也不會建立比較 attempt。
正式 `--scaffold-customer-patch-preparation` 只建立 preparation 目錄、plan、request 草稿與空的 evidence 目錄，
不建立或發布 `rule-sets`。`request` 草稿不能當成 evidence；空的 `rule-sets` 只表示目錄存在，不能視為已發布規則；缺少正式規則或正式 evidence 時，preflight 必須 `Blocked`。

### 規則位置與阻擋原因

`ruleStoreRoot` 只是已發布規則的搜尋根目錄，不是規則本身；目錄存在、目錄非空或存在 approval／pending／publication request 檔案，都不能視為規則已發布。preflight 必須在該根目錄下解析成功的 `CustomerPatchComparison` published rule，並確認其固定 `ContentChecksum`。

CLI 的 `policy.invalid` 是 Customer-Patch Policy 前置驗證的總括阻擋碼，至少涵蓋：規則根目錄不存在或不可讀、沒有已發布規則、規則格式或類型不符、Checksum 缺失或不符，以及規則候選無法唯一解析。回報 `policy.invalid` 時，必須同時說明可辨識的具體原因、`policyChecksum` 是否取得，以及 preflight 是否仍為零寫入；不得把 `rule-sets` 目錄存在回報為規則已就緒。

可貼給 Codex 的最少 Prompt：

```text
請執行 Customer-Patch 比較前置驗證。

案件根目錄：<caseRoot>
Customer-Patch Comparison 已發布規則儲存位置：<ruleStoreRoot>
```

此最少 Prompt 固定執行 `PREFLIGHT_CUSTOMER_PATCH`，並對 plan 中所有
Customer-Patch 比較逐跳執行。只有需要單獨驗證某一跳時，才另外提供 `跳點識別`；
若存在多個準備目錄或無法唯一識別跳點，必須停止，不得依日期或資料夾順序猜測。

Codex 必須：

1. 從唯一準備目錄的 plan 解析 `preparationId`、`comparisonId`、`comparison.source`、`comparison.target`、evidence 與 `comparison-attempts` 相對路徑；不得以版本字串推導任何比較目錄。
2. 確認每一跳已有 `patch-support-evidence.json`，且其輸入 checksum 仍與右側 `patch-support-input` 相同。
3. 僅從 `<ruleStoreRoot>` 解析已發布的 `CustomerPatchComparison` 規則與固定 Checksum；無法解析、未發布或衝突時停止。
4. 對每一跳產生全新的非既有 `attemptId` 供唯讀 preflight 使用，並以 `Package.Cli --preflight-customer-patch-comparison <request.json>` 執行。
5. 回報 `Ready` 或 `Blocked`、Policy Checksum、兩端輸入 checksum 與預期 attempt 路徑。

狀態語意必須固定：只要已呼叫 preflight command 並取得結果，無論結果是 `Ready` 或 `Blocked`，都算「已執行 Customer-Patch 比較前置驗證」；`Blocked` 表示已執行但驗證未通過。只有在 command 尚未呼叫、沒有產生 preflight 結果時，才能回報「尚未執行 preflight」。
`Blocked` 不得描述成「尚未執行」；必須列出阻擋條件，並明確說明未建立 comparison attempt、未進入實際 Package 比較。

不得以左側客戶基準實際版本判定或阻擋此跳；當下 DB 版本驗證只屬於日後 Package Import／DB 升級執行關卡。

`patch-support-evidence.request.json` 只是待登錄草稿，不是 `patch-support-evidence.json`；必須先使用
`codex-customer-patch-evidence-recording-prompt.md` 完成每一跳的受控 evidence 登錄，再執行本節 preflight。
需要準備人工核准與待發布資料時，使用 `codex-customer-patch-rule-publication-preparation-prompt.md`；
再由 `codex-customer-patch-rule-publication-execution-prompt.md` 於具名人工確認後執行受控發布。
只有發布成功後的 `rule-sets\published\...` 規則才可供本節解析；approval draft、pending 說明或 publication request 均不算已發布規則。

本 Prompt 只在指定案件準備目錄的 `manifests` 建立或更新單一登錄草稿，供選用地將跳點登錄到案件 `packageComparisonPreparations` 使用。它不修改 `aras-upgrade-case.json`、不建立 history、不中斷既有流程，也不執行 Package 比較、DB、Import、Rule 2 或升級。不得將案件草稿寫入本專案 `docs/` 或 `rule-sets/`。

人工核對草稿後，可使用 `Package.Cli --register-rule1-preparation <request.json>` 進行受控登錄。此 CLI 會驗證案件與路徑、拒絕重複 `preparationId`，並只追加登錄 history；它不執行 Rule 1 比較。登錄不是 `--prepare-rule1` 的前置條件；只要完整提供跳點與四個相對路徑，即可先行比較。

## OOTB_RULE1 每一跳必填資料

| 欄位 | 說明 |
|---|---|
| `preparationId` | 案件內唯一、安全的跳點識別，例如 `11sp8-to-11sp15`。 |
| `sourceVersion`／`targetVersion` | 此跳完整來源與目標版本；不可相同。 |
| `ootbSourceSolutionsRelativePath` | 案件根目錄下的來源 OOTB `Solutions` 相對路徑。 |
| `ootbTargetSolutionsRelativePath` | 案件根目錄下的目標 OOTB `Solutions` 相對路徑。 |
| `patchSupportInputRelativePath` | 該跳 `patch-support-input` 相對路徑。 |
| `preparationAttemptRelativePath` | 該跳新的 `rule1-attempts` 相對路徑。 |
| `ruleStoreRoot` | 已發布 Rule 1 規則儲存位置；不可填草稿規則或自行推測 Checksum。 |
| `actor` | 具名人工登錄者；CLI 會用它寫入正式 `createdBy` 與 `createdAt`。 |

`customer-package-baseline/<customer-id>/input` 不屬於 Rule 1 預先比較的必填輸入；它保留給後續 Rule 2。

只有在 `workflow: OOTB_RULE1` 時，本節欄位才是必填。`CUSTOMER_PATCH_COMPARISON` 不應套用本節的 OOTB 路徑要求，也不得因缺少這些欄位而建立 Rule 1 草稿。

## 執行分流硬性規則

```text
CUSTOMER_PATCH_COMPARISON + CONFIRM_PATCH_INPUT
  → 確認輸入並更新 patch-support-evidence.request.json。
  → 不建立 package-rule1-registration-draft.md。
  → 不處理 Rule 1 規則庫或 OOTB Solutions。

CUSTOMER_PATCH_COMPARISON + PREFLIGHT_CUSTOMER_PATCH
  → 驗證既有 Customer-Patch evidence，解析已發布規則／Checksum，並執行唯讀 preflight。
  → 要求 Customer-Patch Comparison 已發布規則儲存位置與跳點識別。
  → 不建立 package-rule1-registration-draft.md。
  → 不處理 Rule 1 規則庫或 OOTB Solutions。

OOTB_RULE1 + GENERATE_OOTB_RULE1_DRAFT
  → 執行後續 Rule 1 登錄草稿流程。
  → 不更新 patchSupportSource。

工作流、執行動作缺少或配對不允許
  → 停止並要求補充，不得改走其他分支。
```

## Codex Prompt

```text
請只產生 Package Rule 1 比較的「人工填寫登錄草稿」，不要修改案件或執行任何比較。

本段只允許在 workflow 為 OOTB_RULE1 且執行動作為 GENERATE_OOTB_RULE1_DRAFT 時執行；其他工作流或動作必須停止，不得建立草稿。

先閱讀：
1. AGENTS.md
2. .agents/skills/edit-project-directly/SKILL.md
3. .agents/skills/aras-manage-upgrade-case/SKILL.md
4. .agents/skills/aras-prepare-ootb-hop-diff/SKILL.md
5. docs/standards/AML_Structure_and_Traversal_Standard.md

案件根目錄：<CASE_ROOT>
登錄者：<HUMAN_CREATED_BY>
Rule 1 已發布規則儲存位置：<RULE_STORE_ROOT>
準備目錄名稱：<PREPARATION_DIRECTORY_NAME>

完整跳點：<SOURCE_VERSION> → <TARGET_VERSION>
來源 OOTB Solutions 相對路徑：<OOTB_SOURCE_SOLUTIONS_RELATIVE_PATH>
目標 OOTB Solutions 相對路徑：<OOTB_TARGET_SOLUTIONS_RELATIVE_PATH>
Patch/Support 輸入相對路徑：<PATCH_SUPPORT_INPUT_RELATIVE_PATH>
Rule 1 attempts 相對路徑：<RULE1_ATTEMPTS_RELATIVE_PATH>

可重複提供多個「完整跳點」區塊；不得只輸入目標版本，也不得從目錄名稱、客戶版本、前一跳或 DB 升級路徑猜測來源版本。

請在 `<CASE_ROOT>\package-upgrade\<PREPARATION_DIRECTORY_NAME>\manifests\package-rule1-registration-draft.md` 建立或更新單一 Markdown 草稿。若檔案已存在，只更新由本 Prompt 管理的「案件摘要、每跳 JSON 與未完成項目」區塊，不得覆寫人工補充註記。

已由目錄骨架確定的路徑必須自動填入：

- `package-upgrade/<PREPARATION_DIRECTORY_NAME>/hops/<HOP_SLUG>/ootb-source-solutions`
- `package-upgrade/<PREPARATION_DIRECTORY_NAME>/hops/<HOP_SLUG>/ootb-target-solutions`
- `package-upgrade/<PREPARATION_DIRECTORY_NAME>/hops/<HOP_SLUG>/patch-support-input`
- `package-upgrade/<PREPARATION_DIRECTORY_NAME>/hops/<HOP_SLUG>/rule1-attempts`

草稿的每個跳點包含：

1. `PackageComparisonPreparation` JSON 區塊，欄位必須是：
   `preparationId`、`sourceVersion`、`targetVersion`、
   `ootbSourceSolutionsRelativePath`、`ootbTargetSolutionsRelativePath`、
   `patchSupportInputRelativePath`、`preparationAttemptRelativePath`、`notes`。不得填入 `createdAt`、`createdBy`。
2. Rule 1 輸入確認表：兩端 OOTB Solutions、Patch/Support、輸出 attempt、版本證據、已發布規則版本與 Checksum。
3. `--prepare-rule1` request JSON 草稿；包含 `caseRoot`、`preparationId`、`attemptId`、`actor`、`ruleStoreRoot`、`sourceVersion`、`targetVersion`、`ootbSourceSolutionsRelativePath`、`ootbTargetSolutionsRelativePath`、`patchSupportInputRelativePath`、`preparationAttemptRelativePath`、`safetyWhitelist` 與可選 `notes`、`confirmation`。此 request 可在未正式登錄時直接使用。
4. `--confirm-rule1` request JSON 草稿；只包含 `caseRoot`、`preparationId`、`attemptRelativePath`、`artifactRelativePath`、`actor`、`ruleStoreRoot`、`sourceVersion`、`targetVersion`、`safetyWhitelist` 與可選 `confirmation`。
5. 未完成項目清單；不得將未知值填成已確認、Completed、Approved 或自動推測內容。

安全限制：
- 所有 Package 路徑必須是 `<CASE_ROOT>` 下的相對路徑；拒絕絕對路徑、`..`、空白路徑與輸入／輸出重疊。
- `attemptId` 與 artifact 檔名只能保留為待人工指定值；不得重用或覆寫既有 attempt／ZIP。
- 不得讀取、解析、複製或比較 Package／AML；只根據使用者已輸入的文字建立草稿。
- 不得建立或發布規則、計算或填入 Checksum、建立 DB／Import 指令，或修改 `aras-upgrade-case.json`。
- 只有 OOTB 內容／版本證據、已發布規則版本與 Checksum、attemptId、whitelist、confirmation 與 artifact 路徑可保留 `<待填寫>`；骨架已決定的版本與相對路徑不得保留空白或 `<待填寫>`。
```

## 芯洲目前應填的跳點範例

先填入完整跳點與實際 OOTB `Solutions` 位置，再產生草稿：

```text
11.0 SP8 → 11.0 SP15
11.0 SP15 → 12.0 SP18
12.0 SP18 → R38
```

目前 `patch-support-input` 已存在，但仍須由人工補齊或明確指出每一跳兩端 OOTB `Solutions` 路徑與已發布 Rule 1 規則位置，才可先行預先比較；正式登錄可於比較前或後補做。

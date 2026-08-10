# 50.測試 Core Tree 對話摘要

## 專案與範圍

- 工作目錄：`C:\Users\kenny\OneDrive\文件\Aras Upgrade Orchestrator`
- 不建立或使用 Git worktree。
- 案件範圍：`K:\70.ArasUpgradeCases\Customer1209-To-R38`。
- 三份 Core Tree、R38 與 evidence 保持唯讀；不操作 DB、Aras 工具或外部環境。

## 輸入位置

- CustomerSource：`K:\70.ArasUpgradeCases\Customer1209-To-R38\core-tree\inputs\customer-12sp9\tree`
- OOTBSource：`K:\70.ArasUpgradeCases\Customer1209-To-R38\core-tree\inputs\ootb-12sp9\tree`
- OOTBR38：`K:\70.ArasUpgradeCases\Customer1209-To-R38\core-tree\inputs\ootb-r38\tree`
- Attempts：`K:\70.ArasUpgradeCases\Customer1209-To-R38\core-tree\attempts`

## Skill 順序

1. `$aras-innovator-upgrade`
2. `$aras-manage-upgrade-case`
3. `$aras-validate-core-tree-inputs`
4. `$aras-run-core-tree-comparison`
5. `$aras-compare-core-tree`
6. 回到 `$aras-manage-upgrade-case` 保存追加歷程

## Evidence 範本與規則

每份 evidence 應有 `version-primary.*`、`integrity.*`，以及補充用的 `source-provenance.md`。專案範本位於 `docs/operations/core-tree/templates/`：`version-primary.template.md`、`integrity.template.md`、`source-provenance.template.md`、`preflight-request.template.json`、`comparison-request.template.json`、`manual-review-register.template.md`。

## Preflight

案件曾因文字型 `caseId` 被阻擋；後續使用 GUID `761aae66-84c9-437c-9a3d-709226a382e8` 並補上正式 manifest 欄位。最後正式 preflight 結果為 `Ready`，CLI exit code `0`。三份輸入均有 Client／Server：Customer 929 files、OOTB 12SP9 318 files、OOTB R38 1131 files。

## 正式比較

- AttemptId：`38dc5fda-2d52-4870-a530-1cb598f18a3a`
- Output：`K:\70.ArasUpgradeCases\Customer1209-To-R38\core-tree\attempts\attempt-20260807-001`
- 結果：`Incomplete`
- A：653；B：3；C：28；人工確認：9；Errors：0
- 輸出：`incomplete-manifest.json`、`manual-reviews.json`、`processing-summary.json`
- 歷程：`K:\70.ArasUpgradeCases\Customer1209-To-R38\.orchestrator\history.jsonl`

## 人工確認

9 筆皆為 `CustomerAdditionCollidesWithR38`，涉及：`bcsActionPack.dll`、`bcsActionPack.pdb`、`bcsNotificationLite.dll`、`ICSharpCode.SharpZipLib.dll`、`NPOI.dll`、`NPOI.OOXML.dll`、`NPOI.OpenXml4Net.dll`、`NPOI.OpenXmlFormats.dll`、`NPOI.xml`。

人工登錄位置：`K:\70.ArasUpgradeCases\Customer1209-To-R38\core-tree\attempts\attempt-20260807-001\manual-review-register.md`。範本：`docs/operations/core-tree/templates/manual-review-register.template.md`。目前不得由 AI 自行填寫決定、解除 `Open` 或建立 `Completed`。

## 尚未完成

1. 確認 manifest route 與已核准 Support 目錄證據。
2. 確認三份 `version-primary.md` 的真實版本主證據。
3. 確認三份 `integrity.sha256` 的真實產生來源與範圍。
4. 由責任人逐筆完成 9 筆人工 review。
5. 由正式 command/action 重新判定是否可建立 `Completed`。
6. OneDrive 文件變更尚未 commit／push。

## 原則

不修改 input、R38、evidence 或既有歷程；不依資料夾名稱推定版本／Support；不覆寫 attempt；未完成人工確認時只能維持 `Incomplete`。

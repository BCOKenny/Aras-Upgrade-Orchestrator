---
name: aras-run-core-tree-comparison
description: Use when Codex 需要從簡短要求執行或準備可重複的 Customer1209-To-R38 Core Tree 比較流程，包括前置檢查、A／B／C 比較、安全建立 attempt 及不可變案件歷程。適用於固定案件 K:\70.ArasUpgradeCases\Customer1209-To-R38 的「執行 Core Tree 前置檢查」或「執行 Core Tree 比較」等要求。
---

# 執行 Customer1209-To-R38 Core Tree 比較

只對以下固定案件根目錄使用此 Skill：

`K:\70.ArasUpgradeCases\Customer1209-To-R38`

## 解讀簡短要求

- 收到 `執行前置檢查` 時，只執行 preflight checks；不得建立 attempt 目錄或比較輸出。
- 收到 `執行比較` 時，先執行 preflight checks；只有正式 Core Tree 工作流程允許時才繼續。
- 收到 `列出阻擋原因` 或 `檢查狀態` 時，只讀取並回報；不得修改案件。
- 除非使用者明確要求另一項獨立工作，否則不得修改來源 tree、evidence、R38、database、Aras tool 或程式碼。

## 依序路由

1. 使用 `$aras-innovator-upgrade` 識別目前階段及適用關卡。
2. 使用 `$aras-manage-upgrade-case` 讀取案件身分、執行歷程、retry eligibility、directory lock 及預定的新 attempt 路徑。
3. 使用 `$aras-compare-core-tree` 驗證輸入；只有在獲准時，才執行比較及 A／B／C 分類。
4. 再次使用 `$aras-manage-upgrade-case` 追加不可變執行歷程。不得手動編輯歷程或自行建立 `Completed`。

## 固定輸入

| 角色 | 路徑 |
|---|---|
| CustomerSource | `core-tree\inputs\customer-12sp9\tree` |
| OOTBSource | `core-tree\inputs\ootb-12sp9\tree` |
| OOTBR38 | `core-tree\inputs\ootb-r38\tree` |
| Customer evidence | `core-tree\inputs\customer-12sp9\evidence` |
| OOTB 12SP9 evidence | `core-tree\inputs\ootb-12sp9\evidence` |
| OOTB R38 evidence | `core-tree\inputs\ootb-r38\evidence` |
| 新 attempt 的上層目錄 | `core-tree\attempts` |

所有相對路徑都必須以固定案件根目錄解析。將三份輸入 tree 視為不可變，並拒絕任何輸入與輸出路徑重疊的情況。

## 不可妥協的控制條件

- 使用正式且已受測的 Core Tree command/action；不得手動模擬比較。
- 不得從目錄名稱推測版本。
- 不得預先建立、重用或覆寫 attempt 目錄。
- 遇到多個 R38 candidate 時不得猜測；保留所有 candidate 等待人工確認。
- 遵循 `$aras-compare-core-tree` 及其路由子 Skill 提供的文字／binary 比較與副檔名演進規則。
- 只允許正式工作流程判定 `Incomplete` 或 `Completed`；未解決的人工確認不得自動解除。

## 回報

一律說明目前路由的 Skill、案件根目錄、預定的 attempt 路徑與 lock、已完成關卡、阻擋原因，以及是否發生任何寫入。
## Formal read-only preflight

Use the compiled CLI with `--preflight <request.json>` for a read-only gate before comparison. It invokes `CoreTreeComparisonPreflightCommand` and must not create an attempt, snapshot, directory lock, history event, output, or `Completed`.

Only the formal `CoreTreeComparisonCommand` may start a comparison attempt, obtain the execution lock, create the new output, and append `history.jsonl`.

Formal command/action: preflight must not create an attempt, snapshot, directory lock, history event, output, or `Completed`.

`DirectoryLeaseManager` is reserved for the formal comparison command; preflight only reports the expected lease path.

---
name: aras-prepare-adapted-package
description: 使用已驗證的 Rule 1 TargetDiff 與不可變客戶 Package 基準，安全建立、檢查及完成 Aras Rule 2 跳點正式適配 Package，管理原始 Solutions 備份、雙端 XML 工作副本、七步 Scalar 規則、federated Property、人工確認與不可覆寫完成紀錄。當 Codex 需要準備或驗證特定跳點適配 Package、判斷備份或人工確認是否阻擋跳點執行時使用；不連接 DB、不操作 Core Tree 或未授權 K: 目錄。
---

# 準備正式適配 Package

## 目標

協調 Rule 2 的受控工作區與完成關卡。AML 遞迴、七步規則、備份、封裝驗證及完成判定必須呼叫正式受測核心，不在 Skill 內重建。

## 開始前

1. 讀取根層 `AGENTS.md`、`CONTEXT.md`、相關 ADR、規格第 9、11、12、13 節。
2. 完整讀取 AML Standard、`docs/design/skill-map.md` 與 `references/core-capabilities.md`。
3. 使用 `aras-manage-upgrade-case` 核對案件、跳點、新執行嘗試、目錄鎖與安全白名單。
4. 使用 `aras-manage-upgrade-rules` 取得已發布並固定版本／Checksum 的 Rule 2 規則。
5. 使用 `aras-prepare-ootb-hop-diff` 驗證 Rule 1 ZIP 的版本、雙端內容、完成狀態及封裝 Checksum。

## 固定程序

1. 核對客戶 Package 基準、客戶 `Support\Solutions`、跳點版本與新嘗試目錄；不得從資料夾名稱猜測。
2. 先呼叫 `OotbHopDiffArtifactVerifier`；不可重用時立即停止。
3. 先由 `AdaptedPackageBuilder` 在 `Solutions` 外建立含跳點、時間與嘗試 ID 的完整備份。備份失敗時不得寫入工作副本。
4. 只將客戶基準 XML 建成來源工作副本，只將已驗證 `TargetDiff` XML 建成目的工作副本；非 XML 保留原位置且不比較、不複製、不刪除、不覆寫。
5. 由 `Rule2AdaptationEngine` 依 CompareKey 遞迴 Item Property 與 Relationships，套用已固定的七步 Scalar 規則及 federated Property 例外。
6. 單一 XML 錯誤或人工確認不阻止無相依項目繼續，但整體保持 `Blocked`。外部修正後建立新執行嘗試並重新解析，不得只勾選解除。
7. 只有零錯誤、零人工確認的 `ReadyForFinalization` 結果可交給 `AdaptedPackageFinalizer` 建立不可覆寫完成紀錄。

## 安全責任

- 不得跳過原始 `Solutions` 備份，不得把封存版升級工具誤當此備份。
- 不得修改客戶 Package 基準或 Rule 1 ZIP；不得手工改寫規則快照、Checksum 或完成紀錄。
- 不得把 Item Property 當 Scalar；不得以固定深度遍歷 AML；不得修改 Item attributes。
- 未取得實際客戶目錄授權時，只能使用測試／演練工作區與替身。
- 不連接正式 DB、不啟動 Aras 升級工具、不操作 Core Tree 或 `K:`。

## 輸出

回報嘗試 ID、跳點、Rule 1 封裝 Checksum、Rule 2 規則版本／Checksum、備份位置、雙端工作副本、XML 統計、錯誤／人工確認、候選或完成狀態及完成紀錄。

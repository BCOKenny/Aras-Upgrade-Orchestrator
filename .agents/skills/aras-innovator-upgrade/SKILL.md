---
name: aras-innovator-upgrade
description: 以證據與階段關卡總協調 Aras Innovator 升級，判斷案件階段、讀取案件與不可覆寫執行歷程、選擇適用功能 Skill、管理安全關卡／停止條件／證據／Rollback 與交接。當 Codex 需要跨案件管理、客戶 Package、Rule 1／Rule 2、Core Tree、升級跳點、驗證或最終交付等多能力協調，或需要判定下一個安全動作時使用；功能細節交由對應 Aras 功能 Skill 與正式受測程式處理。
---

# Aras Innovator 升級總協調

## 目標

作為升級工作的唯一總協調入口。判定目前階段與關卡、選擇功能 Skill、保存證據並維持停止與 Rollback 能力；不要在主 Skill 內複製 AML、Package、Core Tree 或案件核心邏輯。

## 開始前

1. 讀取儲存庫根層 `AGENTS.md`、`CONTEXT.md`、相關 ADR 及目前需求規格。
2. 讀取 `references/project-facts.md` 與 `references/terminology.md`。
3. 準備演練、執行、驗證或交付時，讀取 `references/upgrade-checkpoints.md`。
4. 讀取 `docs/design/skill-map.md`，確認功能 Skill 的責任與建置狀態。
5. 若已提供案件根目錄，先路由 `aras-manage-upgrade-case` 讀取案件清單與執行歷程；不得只依資料夾名稱判斷案件。

## 判定目前階段

使用下列階段名稱：盤點、規劃、演練、正式執行、驗證、交接。失敗、非預期結果或中斷時先進入異常處理，不得直接跳到重試。

每次先輸出：

- 本次要求的成果；
- 已知案件、來源與目標版本及環境；
- 目前階段與最後完成的檢查點；
- 阻礙下一關卡的未知事項；
- 將路由的功能 Skill；
- 下一個安全動作。

## 功能路由

| 使用者意圖 | 功能 Skill |
|---|---|
| 建立／開啟案件、路徑、任務、歷程、中斷、重試、安全關卡 | `aras-manage-upgrade-case` |
| 客戶 Package 基準與一次性產生流程 | `aras-build-customer-package` |
| Rule 1 OOTB 跳點差異包 | `aras-prepare-ootb-hop-diff` |
| Rule 2 正式適配 Package | `aras-prepare-adapted-package` |
| 規則草稿、驗證、發布及版本例外 | `aras-manage-upgrade-rules` |
| 比較及分類 Core Tree | `aras-compare-core-tree` |
| DB 跳點、人工登入驗證與備份證據 | `aras-coordinate-upgrade-hop` |
| 最終交付、完成判定及交接 | `aras-assemble-upgrade-delivery` |

可對一個要求依序路由多個功能 Skill，但每次只執行已通過關卡的下一個動作。若 Skill Map 標示對應功能尚未建立，停止在路由邊界，列出缺少的正式能力；不得由主 Skill 即席模擬功能細節。

## 協調程序

1. 使用 `aras-manage-upgrade-case` 驗證案件身分、路徑版本、任務相依與最後執行狀態。
2. 依使用者意圖及目前關卡選擇功能 Skill。
3. 要求功能 Skill 回報輸入快照、前置條件、安全等級、停止條件、輸出及證據位置。
4. 必要條件不足時保留阻擋；單人確認不得取代備份、識別、版本、Checksum 或其他必要證據。
5. 功能結果失敗或中斷時停止相依任務，保存證據並確認最後正常檢查點。
6. 只有具 Idempotency、已完成 Rollback 或已回到指定檢查點時才建立新執行嘗試。
7. 交接前確認關鍵指示、證據與下一步均存在於儲存庫或案件紀錄，不只存在於對話。

## 固定安全責任

- 未取得明確授權，不執行破壞性、不可逆、正式環境、憑證或外部系統操作。
- 不虛構版本專屬指令、相容性、帳密、核准或完成證據。
- 已發生歷程只能追加；錯誤使用更正紀錄，不修改原事件。
- 執行前固定快照；條件改變時重新判定及確認。
- 在驗證結果被接受前持續保留 Rollback 能力。
- AI 不得發布規則、解除阻擋、改變升級路徑或完成判定。
- AI 不得自動讀取或傳送完整客戶 Package、Core Tree 或 Log；只處理操作人員主動選取、遮蔽並預覽的少量片段。
- 涉及 AML 時，先讀取並遵守 `docs/standards/AML_Structure_and_Traversal_Standard.md`；規格衝突時停止。

## 證據與狀態

一致使用「已驗證、相關人員提供、假設、未知、不適用、待確認、待處理、已完成、已阻礙」。只有 Runbook 或功能 Skill 指定的證據存在時才能標記完成。

將穩定且已驗證的專案事實更新至 `references/project-facts.md`；將單次案件資料寫入案件清單、計畫、執行歷程或交接紀錄。新資訊與既有事實或 ADR 衝突時明確提出，不自行選擇。

## 完成與交接

只有各適用功能 Skill 均回報必要產物與證據完成、沒有影響交付正確性的阻擋項目，且技術／業務驗證及交接完成時，才能回報案件完成。否則回報精確狀態、阻擋原因與下一個安全動作。

# 11 Package 整合測試

Type: task
Status: resolved

## 目標

串接客戶 Package 一次性流程、AML、規則版本、Rule 1 OOTB 跳點差異與 Rule 2 正式適配 Package，驗證成功、錯誤及阻擋行為。

## 驗收結果

- 完整成功路徑保留一次性基準證據、固定 Rule 1／Rule 2 規則快照並建立正式完成紀錄。
- 客戶 Package 基準與 OOTB 輸入保持不可變，非 XML 遵守資料邊界。
- Rule 1 ZIP 遭竄改時在備份及 `Solutions` 寫入前阻擋。
- Rule 2 人工確認未解除時不得建立完成紀錄。
- 新增跳點身分一致性關卡，禁止將正確差異包錯套到另一版本區間。

## Comments

2026-08-03：4E 整合測試完成；全部使用隔離本機測試資料，未操作正式 DB、正式 Package、Core Tree 或 K:。

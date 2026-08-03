# 06－Skill 三層架構與案件管理功能 Skill

Type: task  
Status: resolved

建立「一個主 Skill＋約八個功能 Skill＋功能 Skill 內細項執行單元」架構，並在第一階段完成 Skill Map、主 Skill 路由及 `aras-manage-upgrade-case`。

## 驗收

- Skill Map 定義各 Skill 的觸發條件、輸入、輸出、安全責任及相依關係。
- 主 Skill 只負責總協調、階段、路由、關卡、停止條件、證據及交接，不複製功能細節。
- `aras-manage-upgrade-case` 能獨立處理案件清單、路徑、任務圖、歷程狀態及受控執行判定。
- Skill 引用正式受測核心，不以 Markdown 另建一套業務邏輯。
- 未實作的功能 Skill 必須清楚阻擋，不得由主 Skill 即席模擬。

## Comments

- 2026-08-03：需求方確認 Skill 架構須與程式同步實作，開始補入第一階段。
- 2026-08-03：完成 Skill Map、主 Skill 路由、`aras-manage-upgrade-case`、正式核心能力對照及三項 Skill 架構情境測試；整體 Release 驗證 15／15 通過。
- 2026-08-03：`quick_validate.py` 因本機與內建 Python 均缺少 PyYAML 無法啟動；已依其原始碼將等同的 frontmatter、命名、description 與 metadata 契約納入零外部套件測試。

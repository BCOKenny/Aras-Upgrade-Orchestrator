# 0002：三層 Skill 架構與正式核心分工

狀態：已採用  
日期：2026-08-03

## 背景

升級協調同時包含案件、Package、AML、規則、Core Tree、DB 跳點與交付。若主 Skill 包含所有細節，會造成觸發範圍過大、責任重疊及與正式程式規則分歧；若每個微小步驟都建立 Skill，則無法維持清楚的業務與安全邊界。

## 決策

- 採用一個主 `aras-innovator-upgrade`、約八個可獨立驗收功能 Skill，以及功能 Skill 內低自由度細項執行單元。
- 主 Skill 只負責階段、路由、安全關卡、停止條件、證據、Rollback 與交接。
- 功能 Skill 不複製另一套核心邏輯，必須呼叫或引用正式受測 command/action；缺少正式執行面時停止，不手工模擬持久化或高風險動作。
- 只有能被獨立要求、具有完整輸入輸出及獨立安全邊界的能力，才升格為頂層 Skill。
- 功能程式與對應 Skill 同步建立及驗收，不等全部程式完成後才補 Skill。

## 影響

- 第一階段立即建立 Skill Map、調整主 Skill，並建立 `aras-manage-upgrade-case`。
- 後續 Package／AML、Core Tree、跳點與交付開發必須在同一階段建立對應功能 Skill。
- 主 Skill 遇到尚未建立的功能時要明確阻擋，不得即席重建細節流程。
- 詳細名稱、輸入、輸出、安全責任及相依關係以 `docs/design/skill-map.md` 為準。

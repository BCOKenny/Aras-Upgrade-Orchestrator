---
name: aras-build-customer-package
description: 安全建立、檢查及追蹤 Aras 客戶 Package 一次性基準產生流程，管理首次 DB 變更前鎖定、固定 action／版本／Checksum、人工 DB 還原證據、Aras Export 排除處置與永久完成狀態。當 Codex 需要準備客戶 Package 基準、判斷一次性流程能否開始或重試、檢查 Rollback／Export 證據時使用；不負責 AML、Rule 1／Rule 2、Core Tree 或自行操作正式 DB 與 Aras Export。
---

# 建立客戶 Package 基準

## 目標

協調只能安全執行一次的客戶 Package 基準產生流程。所有狀態判定均呼叫或引用正式受測核心；不要在 Skill 內複製鎖定、Checksum 或完成判定邏輯。

## 開始前

1. 讀取根層 `AGENTS.md`、`CONTEXT.md`、相關 ADR 與 `.scratch/aras-upgrade-orchestrator/spec.md` 第 4、5、7、15、18 節。
2. 讀取主 Skill 的 `references/project-facts.md`、`references/terminology.md` 與 `docs/design/skill-map.md`。
3. 讀取 `references/core-capabilities.md`，確認正式程式入口與尚未提供的執行面。
4. 先路由 `aras-manage-upgrade-case` 核對案件、任務、最新嘗試及不可覆寫歷程。

## 固定程序

1. 確認原始 Package 人工備份與 DB 備份均有識別及證據。
2. 首次 DB 變更前，由 `CustomerPackageOneTimeFlow` 建立鎖定，綁定案件、任務、流程嘗試、環境、目標、備份與核准 action。
3. 只允許固定清單內、版本及 Checksum 與鎖定相符的動作通過 `CustomerPackageActionGate`。
4. 匯入客戶 DB 前仍須經受控執行的單次確認；鎖定不能取代確認。
5. Aras Export 由操作人員人工執行；記錄工具版本、輸出、結果及所有取消選取項目。
6. 每個取消選取項目均須有處置與證據；未處置時保持 `Locked`。
7. 成功時由正式能力追加 `Completed`；此狀態永久不可重開。

## 失敗與 Rollback

- 失敗或中斷不解除 `Locked`，也不自動續跑。
- DB Rollback 由操作人員人工執行。
- 只有備份識別完全相符且還原證據存在時，固定規則才能追加 `RolledBack`。
- `RolledBack` 後才能建立新流程嘗試；不得手工改寫 `history.jsonl`、狀態或案件檔案來模擬解鎖。

## 安全責任

- 不得產生、修改或自由組合 SQL。
- 不得連接客戶正式 DB、不啟動或自動操作 Aras Export。
- 預設外部執行器保持阻擋；沒有明確授權與正式 adapter 時停止在介面邊界。
- AI 不得鎖定、解鎖、標記 Rollback、接受排除風險或完成流程。
- 不解析或比較 AML，也不處理 Rule 1、Rule 2、Core Tree 或正式適配 Package。

## 輸出

回報案件與流程嘗試識別、目前狀態、綁定備份、核准 action、證據、未處置排除項目、阻擋原因及下一個安全動作。

# 14 Core Tree command/action 與測試 CLI

Type: task
Status: resolved
Blocked by: 13

## Scope

建立正式 `CoreTreeComparisonCommand`，將 Skill 協調契約落實為案件讀取、版本／證據驗證、不可覆寫快照、SafetyPolicy、目錄鎖、ExecutionAttemptService、CoreTreeComparisonBuilder、append-only history 與固定結果模型；另建立離線 JSON 測試 CLI。

## Answer

已完成。正式 command 只接受隔離案件與已驗證 Core Tree 輸入；未通過 SafetyPolicy 或版本證據不符時回傳 `Blocked` 且不建立 attempt。離線 CLI 支援 `--request <request.json>` 與固定 JSON 結果輸出；未接入 UI、DB、Aras Export、登入或升級工具。

## Comments

- 2026-08-05：72/72 offline core tests 通過；Release build 0 warning / 0 error；CLI `--help` 已驗證。

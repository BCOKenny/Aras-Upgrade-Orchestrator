# 13 Core Tree 受控執行入口 Skill

Type: task
Status: resolved
Blocked by: 12

## Scope

建立 `aras-run-core-tree-comparison`，將案件讀取、版本證據、不可覆寫執行快照、新嘗試、安全政策、目錄鎖、歷程與固定結果定義成獨立執行協調 Skill。比較與分類仍由 `aras-compare-core-tree` 負責。

## Answer

Skill、能力映射與 metadata 已建立，正式 `CoreTreeComparisonCommand` 與離線測試 CLI 已建立，契約測試通過。UI、DB、Aras Export、登入與升級工具仍未接入；安全或版本不符時 command 回傳 `Blocked`，Skill 不得自動產生程式或操作外部環境。

## Comments

- 2026-08-05：以先 RED 後 GREEN 的契約測試完成 69/69 offline core tests。

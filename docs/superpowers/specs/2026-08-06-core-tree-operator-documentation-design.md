# Core Tree 操作文件設計

## 目標

為非程式人員提供案件準備與受控 Core Tree 比較的操作文件。交付包含填表範本、Runbook、結果檢查方式及程式與文件對照。

## 範圍

- 正式案件輸入為 `<case-root>/aras-upgrade-case.json`。
- 每個輸入證據目錄必須有 `version-primary.*` 與 `integrity.*`；來源追溯為補充。
- `--preflight` 回傳 `Ready` 前不得準備 `--request`。
- 輸入與既有 attempt 均不可變；結果、人工 review、更正與 backup reference 均須保存為證據。
- 文件不連接 DB、不登入、不呼叫 Aras 工具、不修改 Support/Solutions 或目標 Core Tree。

## 文件結構

`docs/operations/case-management/` 提供案件遷移指引；`docs/operations/core-tree/` 提供 Runbook、檢查方式、程式對照與範本；`docs/standards/` 提供受控強制規範。所有範例均使用通用受控路徑，不含實際案件資料。

# 案件遷移操作指引

## 用途

本文件用於準備案件遷移申請。申請僅是規劃證據；正式 Core Tree command 讀取 `<case-root>/aras-upgrade-case.json`，不讀取 `.scratch/case-migration-request.json`。

## 填表步驟

1. 將 `templates/case-migration-request.template.json` 複製到來源案件之外的位置。
2. 填寫 `caseRoot`、`newCaseId`、`customerCode`、`sourceVersion`、`targetVersion`、`routeVersion` 與每一個 hop。
3. `requestedAt` 必須使用有時區偏移的 ISO 8601 格式，例如 `2026-08-06T14:30:00+08:00`。
4. 在 `confirmation` 記錄人工確認位置；不得在申請內放入密碼、token 或 connection string。
5. 在執行 Core Tree preflight 前，另行建立或驗證正式 `aras-upgrade-case.json`。

## 檢查方式

- `sourceVersion` 與 `targetVersion` 必須與正式 case manifest 及 Core Tree input evidence 一致。
- 每個 hop 必須有非空白的 `supportDirectory` 證據參考。
- 案件遷移申請不可取代 case manifest 或 CLI request JSON。
- 本文件不授權 DB、Aras Export、Support/Solutions 或正式 Core Tree 操作。

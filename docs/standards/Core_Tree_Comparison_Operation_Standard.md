# Core Tree 比較受控操作規範

| 項目 | 內容 |
|---|---|
| 規範狀態 | 受控操作規範 |
| 版本 | 1.1 |
| 生效日 | 2026-08-11 |
| 核准依據 | 專案負責人指示：「將操作指引納入受控規範」 |
| 適用範圍 | 受控 Core Tree 比較的準備、前置檢查、request 執行、結果判讀及證據保存。 |

## 強制規則

1. `<case-root>/aras-upgrade-case.json` 是唯一正式案件依據；案件遷移申請或對話指示不可取代它。
2. 必須使用客戶、來源 OOTB、目標 OOTB 三份彼此分離的唯讀輸入；每份皆須有 `Innovator/Client` 與 `Innovator/Server`。
3. 每個輸入證據目錄必須各有一個以 `version-primary.` 開頭及一個以 `integrity.` 開頭的檔案。`source-provenance.md` 僅為補充證據。
4. 僅在對應 `--preflight` 的結果為 `Ready` 後，才可準備 `--request`。
5. 輸入、snapshot、attempt output、history 與產生的 manifest 都是不可覆寫證據。重試必須建立新的 request 與新的 attempt。
6. `Incomplete`、`Blocked` 與 `Failed` 均為停止條件；不得修改 JSON、history 或 manifest 而將其改成 `Completed`。
7. 正式比較 request 必須有可識別的 `actor`、明確的 `safetyWhitelist`、前置證據與人工確認參考。
8. 人工路徑對應、碰撞、無法讀取檔案及 A/B/C 分類決定都必須寫入 review register；任何未結案 review 均禁止 `Completed`。
9. 本規範不授權 DB 存取、登入、Aras Export、Support/Solutions 變更、目標 Core Tree 修改或其他外部操作。

## Attempt 產物遺失或完整性失敗

1. 若 attempt output 中的 `manual-reviews.json`、`manual-review-register.md`、manifest、summary 或其他正式產物遺失、被修改或 checksum 不符，該 attempt 必須視為 `ArtifactIntegrityFailed`，並永久維持 `Incomplete`；不得補寫、覆寫、刪除後重建，或將其改為 `Completed`。
2. 復原只能由正式 command 建立新的 attempt。執行前必須重新通過 preflight、重新驗證三份 input 的完整性，並提供 `VerifiedIdempotency` retry evidence，說明原 attempt、遺失原因、責任人與新 output root。正式 history 必須追加此復原／retry 的關聯證據，舊 attempt 與既有 history 不得變更。
3. 正式 command 成功建立 attempt 產物後，`manual-reviews.json`、manifest、summary 與其他正式 JSON 產物必須受到受控保護，不得由人工新增、修改、刪除或以檔案總管補回。`manual-review-register.md` 是唯一例外，人工僅可填寫 `Decision`、`Approver`、`Approved at` 與 `Status`；核准 command 必須驗證其固定欄位仍與 `manual-reviews.json` 一致。無法套用所需保護時，必須在結果中記錄原因並維持 `Incomplete`。

## 受控操作文件

- [Core Tree Runbook](../operations/core-tree/runbook.md)
- [Core Tree 結果檢查方式](../operations/core-tree/verification-guide.md)
- [程式與文件對照表](../operations/core-tree/program-document-map.md)
- [Core Tree 範本總覽](../operations/core-tree/README.md)

## 優先順序與衝突處理

`AGENTS.md`、本規範與程式強制安全控制依序適用，並同時遵守更具體的強制標準。若操作文件與本規範或 command 結果衝突，必須停止操作、保存證據並記錄衝突；程式安全控制是最終執行關卡。

## 修訂規則

變更必須記錄新版本、生效日、核准依據；如影響操作人員的行動，必須同步修訂 Runbook、檢查方式及範本。

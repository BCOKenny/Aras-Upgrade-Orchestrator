# Core Tree 比較操作文件

> 規範狀態：受控操作規範（版本 1.0）。強制規則請見 [Core_Tree_Comparison_Operation_Standard.md](../../standards/Core_Tree_Comparison_Operation_Standard.md)。

本目錄提供受控 Core Tree 比較 command 的操作範本、Runbook 與結果檢查方式。操作人員必須先閱讀本文件與受控規範，再填寫 request 或執行 CLI。

## 案件目錄位置

```text
<case-root>/
  aras-upgrade-case.json
  core-tree/
    inputs/<customer|source-ootb|target-ootb>/tree/Innovator/{Client,Server}/
    inputs/<customer|source-ootb|target-ootb>/evidence/
    requests/
    reviews/
    attempts/
```

所有輸入皆為唯讀。`outputRoot` 與每個 attempt output 都必須位於所有 input root 之外。

## 填表範本

| 檔案 | 用途 |
|---|---|
| `preflight-request.template.json` | 執行 `--preflight` 前填寫。 |
| `comparison-request.template.json` | 執行 `--request` 前填寫。 |
| `version-primary.template.md` | 一份輸入的版本證據。 |
| `integrity.template.md` | 一份輸入的完整性證據。 |
| `source-provenance.template.md` | 補充來源追溯；不可取代前兩份必要證據。 |
| `manual-review-register.template.md` | 人工路徑對應或分類決定紀錄。 |

每個 evidence 目錄都必須有檔名以 `version-primary.` 開頭的檔案，以及檔名以 `integrity.` 開頭的檔案。請依 [runbook.md](runbook.md) 執行，並依 [verification-guide.md](verification-guide.md) 判讀結果。

JSON 範本中的 `prerequisites` 必須是物件（例如 `{}`），`retryEvidence` 必須是 `null` 或完整的 retry evidence 物件；不可填入陣列。`comparison-request.template.json` 的 `safetyWhitelist` 必須使用完整 whitelist 物件，不能只填輸出路徑。

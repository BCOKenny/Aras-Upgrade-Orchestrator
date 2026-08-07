# Core Tree 結果檢查方式

> 規範狀態：本文件是 [Core_Tree_Comparison_Operation_Standard.md](../../standards/Core_Tree_Comparison_Operation_Standard.md) 版本 1.0 下的受控結果判讀程序。

## Preflight 結果

| 狀態 | 操作人員處置 |
|---|---|
| `Ready` | 可準備正式比較 request。 |
| `Incomplete` | 補齊缺少的證據，常見為 `version-primary.*` 或 `integrity.*`，然後重新 preflight。 |
| `Blocked` | 修正 manifest、版本、路徑、Client/Server 結構、輸出隔離、規則或 history 問題，然後重新 preflight。 |

## 比較結果

| `commandStatus` | 操作人員處置 |
|---|---|
| `Completed` | 保存 manifest、history、snapshot digest 與 review 紀錄，交由下一個案件關卡。 |
| `Incomplete` | 保存產出並處理每一項 review 或錯誤；不得重新標記為完成。 |
| `Blocked` | 修正訊息指出的安全或輸入條件，並建立新的 request 與 attempt。 |
| `Failed` | 保存受控錯誤與 history；僅依案件安全規則重試。 |

## CLI 錯誤

| Code | Exit code | 檢查方式 |
|---|---:|---|
| `CliArgumentError` | 2 | 必須使用 `--preflight <request.json>` 或 `--request <request.json>`。 |
| `CliInputError` | 1 | 檢查 JSON 語法、必要欄位、路徑與 evidence。 |
| `CliUnexpectedError` | 1 | 停止並保存 JSON 結果與 case history，交由維護人員檢查。 |
| `CliCancelled` | 1 | 視為中斷；不可重用原 attempt output。 |

## 完成前檢查

- 三份已驗證輸入都有兩個必要 evidence 檔名開頭。
- 每份輸入都有 Client 與 Server 結構。
- 每個 Server rule 都是標準相對路徑，且具備必要 checksum evidence。
- 未結案 manual review、`MultipleTargetMappings`、`CustomerAdditionCollidesWithTarget` 與 `FileReadError` 都禁止 `Completed`。
- 結果、人工決定、更正與 backup reference 都是 append-only evidence，不得覆寫。

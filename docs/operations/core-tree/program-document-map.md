# Core Tree 程式與操作文件對照

> 規範狀態：本文件是 [Core_Tree_Comparison_Operation_Standard.md](../../standards/Core_Tree_Comparison_Operation_Standard.md) 版本 1.0 下的受控參考文件。

| 程式元件 | 受控責任 | 操作文件 |
|---|---|---|
| `CoreTreeComparisonPreflightCommand` | 驗證案件、版本、輸入、證據、輸出隔離與規則。 | `runbook.md`、`verification-guide.md` |
| `CoreTreeComparisonCommand` | 建立 snapshot 與 attempt、套用 safety 與 directory lease、追加 history、回傳固定結果。 | `runbook.md`、request 範本 |
| `CoreTreeComparisonBuilder` | 產生 A/B/C 結果檔案及 completion/incomplete manifest。 | `verification-guide.md` |
| `CoreTreeComparisonEngine` | 套用分類與 review 規則。 | `manual-review-register.template.md` |
| `CoreTreeContentComparer` | 套用 Client/Server 文字或二進位比較。 | `verification-guide.md` |
| `CoreTreeLogicalPathResolver` | 解析邏輯對應及多候選 review。 | `manual-review-register.template.md` |
| `Program.cs` | 提供 `--preflight`、`--request`、固定 JSON 與 exit code。 | `runbook.md`、`verification-guide.md` |

操作文件只能協助填寫及判讀 request，不能繞過 `SafetyPolicy`、`DirectoryLeaseManager`、`ExecutionSnapshot`、`ExecutionAttemptService` 或 `AppendOnlyHistoryStore`。

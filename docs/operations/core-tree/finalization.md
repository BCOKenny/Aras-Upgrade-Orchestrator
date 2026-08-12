# Core Tree 比較完成判定

## 適用範圍

`CoreTreeComparisonFinalizationCommand` 將已核准、零錯誤的 Incomplete 比較轉為獨立的完成收據。它不改寫原 comparison attempt，不補造該 attempt 未產生的 A/B/C 檔案，也不操作 Aras、DB 或外部環境。

## 必要輸入

- 原 comparison output 下的 `incomplete-manifest.json`、`processing-summary.json`、`manual-reviews.json` 與 `manual-review-register.md`。
- `CoreTreeManualReviewApprovalCommand` 正式建立的 `manual-review-approval.json`。
- append-only history 中相同 attempt 的 `attempt.incomplete` 與 `core-tree.manual-reviews.approved`。
- 尚不存在且位於 `<case-root>/core-tree/completions` 下的 `completionOutputRoot`。

## 執行方式

先複製並填寫 `templates/completion-request.template.json`，再直接執行已編譯的 CLI DLL：

```powershell
dotnet tools/ArasUpgradeOrchestrator.CoreTree.Cli/bin/Release/net8.0/ArasUpgradeOrchestrator.CoreTree.Cli.dll --finalize-comparison <request.json>
```

成功時建立：

```text
<completionOutputRoot>/completion-manifest.json
```

並在案件 history 追加唯一的 `core-tree.comparison.completed`。同一 comparison attempt 不得重複完成。

## 驗證與阻擋條件

- completion receipt 的 `state` 必須是 `Completed`，`completionKind` 必須是 `ComparisonReviewFinalization`。
- case ID、attempt ID、comparison root、review 路徑與核准收據必須完全一致。
- review JSON 與 register 的目前 SHA-256 必須符合核准收據；核准後遭修改即阻擋。
- comparison 的 `ErrorCount` 必須為零；manifest、summary 與 review count 必須一致。
- `artifactChecksums` 必須固定五份正式輸入：incomplete manifest、processing summary、manual reviews、review register 與 approval receipt。
- completion output 已存在、history 缺事件、已有完成事件或 safety prerequisite 不成立時，結果只能是 `Blocked`。

## 語意邊界

此完成收據表示「比較及人工判定流程完成」。原 attempt 仍保持 Incomplete 且不可覆寫；若需要完整 A/B/C 交付副本，必須使用另外的受控交付流程，不得以 completion receipt 取代。

# Core Tree execution capability map

This Skill coordinates the case boundary; it does not replace the formal core implementation.

| Execution responsibility | Formal capability | Contract |
|---|---|---|
| Read and validate the case | `CaseStore`, `CaseManifest` | Load the immutable case identity, route, inputs, and Core Tree settings. |
| Freeze the attempt input | `ExecutionSnapshot` | Capture action, versions, target, input paths, tool identity, and checksums without overwrite. |
| Track retries and history | `ExecutionAttemptService`, `AppendOnlyHistoryStore` | Create a new attempt and append start/result/interrupted/error events to `history.jsonl`. |
| Enforce execution safety | `SafetyPolicy`, `DirectoryLeaseManager` | Block unsafe roots, overlapping attempts, and unapproved external actions. |
| Compare and build output | `CoreTreeComparisonBuilder` | Validate inputs, compare Client/Server, classify A/B/C, and produce Incomplete/Completed output. |
| Formal entry point | `CoreTreeComparisonCommand` | Established command/action that binds the case snapshot and invokes the builder. It returns a fixed result model; the offline CLI is the test adapter. |
| Import human review approval | `CoreTreeManualReviewApprovalCommand` | Validates the original Incomplete manifest, formal review JSON, and exact resolved register rows; writes a new approval receipt and appends history. It never rewrites the attempt or creates Completed. |
| Finalize an approved comparison | `CoreTreeComparisonFinalizationCommand` | Validates the Incomplete attempt, zero errors, approval receipt, review checksums, and append-only history; writes a new completion receipt under `core-tree/completions` and appends `core-tree.comparison.completed`. It never rewrites the original attempt. |
| Build independent A/B/C delivery | `CoreTreeDeliveryCommand` | Requires an approved comparison completion receipt, immutable classification snapshot, unchanged input tree digests, and formal manual-review decisions; writes one new delivery under `core-tree/deliveries` and appends `core-tree.delivery.completed`. |

The formal command/action and offline CLI are present. UI and external Aras/DB adapters are not present. Core-only tests and the CLI use isolated fixtures and do not constitute a customer execution.

## Attempt 終態與重試

- 比較結果為 `Incomplete` 時，`CoreTreeComparisonCommand` 必須追加 `attempt.incomplete`，不得追加 `attempt.succeeded`。
- `Incomplete` 只能在提供 `VerifiedIdempotency` retry evidence 後，以不存在的新 output root 建立新的 attempt；不得重用或覆寫舊 attempt。
- 比較結果為 `Completed` 時才追加 `attempt.succeeded`；此終態永久禁止重跑，即使提供 retry evidence 亦不得解除。
- 相容舊版時，若既有 `attempt.succeeded` 的正式 evidence reference 是 `incomplete-manifest.json`，讀取狀態視為 `Incomplete`。原事件、舊 attempt 及其產物保持不變。

## Read-only preflight capability

| Capability | Contract |
|---|---|
| `CoreTreeComparisonPreflightCommand` | Read-only validation of the case manifest, three input roots, evidence quality, Client/Server structure, versions, rule checksum, path isolation, file counts, and last history event. Returns `Ready`, `Incomplete`, or `Blocked`; never creates attempt, snapshot, lock, history, output, or `Completed`. |
| CLI `--preflight <request.json>` | Direct compiled-DLL adapter. Exit `0` means `Ready`/`Incomplete`, `2` means `Blocked`, and `1` means malformed request or CLI error. |
| CLI `--finalize-comparison <request.json>` | Finalizes only an approved, zero-error Incomplete comparison into a new completion receipt. Exit `0` means `Completed`, `2` means `Blocked`, and `1` means malformed request or CLI error. |
| CLI `--build-delivery <request.json>` | Builds a new A/B/C delivery only from a completed comparison receipt and verified immutable evidence. Exit `0` means `Completed`, `2` means `Blocked`, and `1` means malformed request or CLI error. |

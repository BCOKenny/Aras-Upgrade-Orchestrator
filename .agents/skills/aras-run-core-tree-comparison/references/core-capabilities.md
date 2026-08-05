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

The formal command/action and offline CLI are present. UI and external Aras/DB adapters are not present. Core-only tests and the CLI use isolated fixtures and do not constitute a customer execution.

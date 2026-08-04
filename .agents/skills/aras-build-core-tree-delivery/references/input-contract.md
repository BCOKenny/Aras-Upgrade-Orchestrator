# 輸入契約

契約版本固定為 `core-tree-capabilities/1`，能力名稱為 `aras-build-core-tree-delivery`。只接受已由 `aras-validate-core-tree-inputs` 驗證的 evidence、`aras-classify-core-tree-differences` 產生的分類結果，以及不存在的 output attempt。

語言中立 fixture request：

```json
{
  "classificationResult": { "status": "ReadyToComplete", "items": [] },
  "customerFiles": { "Client/example.js": { "base64": "" } },
  "sourceOotbFiles": { "Client/example.js": { "base64": "" } },
  "targetOotbFiles": { "Client/example.ts": { "base64": "" } },
  "outputState": { "attemptExists": false, "attemptId": "attempt-001" },
  "evidence": { "inputIds": ["customer", "source-ootb", "target-ootb"], "ruleVersion": "server-text/1", "ruleChecksum": "fixture-server-rules" }
}
```

- `classificationResult.status` 為 `ReadyToComplete` 才能評估 `Completed`；完整交付請求的 `Blocked` 分類結果也必須交給本 Skill 建立診斷用 `Incomplete`，不得執行後續升級工作。
- 每個 item 必有 `classification`（A／B／C）、`sourceRelativePath`；C 必有唯一 `targetRelativePath`。
- file map key 必須是相對於 `Innovator` 的安全 `/` 路徑，value 的 `base64` 是原始 bytes。
- `outputState.attemptExists=true` 表示目標已存在；能力不得寫入、清理或重新利用該目錄。
- 三份 file maps、evidence、rules 與分類結果皆為 immutable inputs；本能力不重新分類。

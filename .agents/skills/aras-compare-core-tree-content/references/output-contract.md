# 輸出契約

結果使用 `docs/design/core-tree-capability-contract.md` 的共用 envelope：

```json
{
  "contractVersion": "core-tree-capabilities/1",
  "capability": "aras-compare-core-tree-content",
  "status": "Compared|Blocked",
  "result": { "comparison": "Equal|Different", "mode": "Text|Binary|BinaryFallback" },
  "messages": [],
  "evidence": { "inputIds": ["left", "right"], "ruleVersion": "", "ruleChecksum": "" }
}
```

- `Compared`：比較已完成；`comparison` 必為 `Equal` 或 `Different`，`mode` 為實際使用模式。
- `Blocked`：輸入、規則或檔案讀取不符合要求；`result` 不得假裝產生比較結論，並以 `messages.kind: "Error"` 及穩定代碼說明。
- Text 解碼失敗不是 `Blocked`：使用 `BinaryFallback`，並加入 `kind: "Notice"`、`code: "TextDecodeFallback"` 的訊息。
- `evidence.ruleVersion` 與 `evidence.ruleChecksum` 必為實際採用的 pinned 規則；所有訊息以 `relativePath` ordinal-ignore-case、再以 `code` ordinal 排序。

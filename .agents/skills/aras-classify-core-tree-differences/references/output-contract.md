# 輸出契約

結果使用 `docs/design/core-tree-capability-contract.md` 的共用 envelope：

```json
{
  "contractVersion": "core-tree-capabilities/1",
  "capability": "aras-classify-core-tree-differences",
  "status": "ReadyToComplete|Blocked",
  "result": {
    "items": [
      {
        "classification": "A|B|C",
        "sourceRelativePath": "Client/example.js",
        "targetRelativePath": null
      }
    ]
  },
  "messages": [],
  "evidence": { "inputIds": [], "ruleVersion": "", "ruleChecksum": "" }
}
```

- item 只可為 A、B 或 C。A、B 的 `targetRelativePath` 為 `null`；C 必須是唯一目標路徑。
- unchanged 不產生 item。
- `ManualReview` 使用 `CustomerAdditionCollidesWithTarget` 或 `MultipleTargetMappings`；`Error` 使用 `FileReadError`。訊息含 source relative path 與足以人工判讀的 `details`。
- items 與 messages 均依 source relative path ordinal-ignore-case、原始 ordinal 次要排序；同一路徑訊息再以 `code` ordinal 排序。
- 任一 `ManualReview` 或 `Error` 使 `status` 為 `Blocked`。本能力不輸出 `Incomplete` 或 `Completed`。

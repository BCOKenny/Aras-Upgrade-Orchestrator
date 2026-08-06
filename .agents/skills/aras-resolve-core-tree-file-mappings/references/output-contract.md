# 輸出契約

結果使用 `docs/design/core-tree-capability-contract.md` 的共用 envelope：

```json
{
  "contractVersion": "core-tree-capabilities/1",
  "capability": "aras-resolve-core-tree-file-mappings",
  "status": "Resolved|Blocked",
  "result": {
    "mapping": "None|Unique|Ambiguous",
    "candidates": [],
    "appliedEvolution": null
  },
  "messages": [],
  "evidence": { "inputIds": [], "ruleVersion": "", "ruleChecksum": "" }
}
```

- `None` 與 `Unique` 的 `messages` 為空；`None` 的 `candidates` 為空、`appliedEvolution` 為 `null`。
- `Unique` 的 `candidates` 只有一個目標相對路徑；exact path 的 `appliedEvolution` 為 `null`。
- `Ambiguous` 使用 `status: "Blocked"`，保留全部穩定排序候選，並輸出 `ManualReview`／`MultipleTargetMappings`；不得自動選擇。
- `evidence.inputIds` 至少包含來源檔案與目標 OOTB 識別；規則版本與 Checksum 必須是實際採用值。

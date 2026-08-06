# 輸出契約

結果遵守共用 envelope，`capability` 固定為 `aras-build-core-tree-delivery`，並使用 `Incomplete` 或 `Completed`。

```json
{
  "contractVersion": "core-tree-capabilities/1",
  "capability": "aras-build-core-tree-delivery",
  "status": "Completed",
  "result": {
    "outputFiles": [
      { "relativePath": "C/CustomerSource/Client/example.ts", "checksum": "SHA-256" }
    ],
    "manifestFiles": ["completion-manifest.json"]
  },
  "messages": [],
  "evidence": { "inputIds": [], "ruleVersion": "", "ruleChecksum": "" }
}
```

- `outputFiles` 只列交付複本，使用 forward slash，依 ordinal-ignore-case path 穩定排序；每筆 `checksum` 是檔案 bytes 的 SHA-256 大寫十六進位。
- `Completed` 的 `manifestFiles` 必含且只含 `completion-manifest.json`；`Incomplete` 必含且只含 `incomplete-manifest.json`，兩種 manifest 不得同時存在。
- `OutputAttemptAlreadyExists` 時不建立 result files 或 manifest，並回傳 `Error` message。
- 交付摘要可額外保存處理計數與輸入 evidence，但不得取代上述 manifest 與穩定 checksum。

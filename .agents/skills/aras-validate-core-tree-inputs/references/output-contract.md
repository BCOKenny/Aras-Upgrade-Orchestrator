# 輸出契約

結果使用共用 envelope，欄位與語意依 `docs/design/core-tree-capability-contract.md`；JSON property 的排列順序不影響比較。

```json
{
  "contractVersion": "core-tree-capabilities/1",
  "capability": "aras-validate-core-tree-inputs",
  "status": "Validated|Blocked",
  "result": { "validatedInputs": [] },
  "messages": [],
  "evidence": {
    "inputIds": [],
    "ruleVersion": "",
    "ruleChecksum": ""
  }
}
```

- `Validated`：全部前置條件符合；`result.validatedInputs` 以 `customer`、`sourceOotb`、`targetOotb` 固定順序列出已驗證 `rootId`。
- `Blocked`：至少一項前置條件不符合；`result.validatedInputs` 為空，並以 `messages.kind: "Error"` 提供所有已判定的穩定代碼。
- `evidence.inputIds` 使用同一固定順序；`ruleVersion` 與 `ruleChecksum` 記錄實際收到的規則證據，無法取得時以空字串表示。
- `relativePath` 若需要指出結構或規則，使用相對於 `Innovator` 的 forward-slash 路徑，例如 `Server` 或 `Server/method-config.xml`。

所有訊息依 `relativePath` ordinal-ignore-case 排序，同一路徑再依 `code` ordinal 排序。

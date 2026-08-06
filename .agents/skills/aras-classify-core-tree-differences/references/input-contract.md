# 輸入契約

契約版本固定為 `core-tree-capabilities/1`，能力名稱固定為 `aras-classify-core-tree-differences`。只接受已由 `aras-validate-core-tree-inputs` 驗證的三份 immutable Core Tree 與同一份版本／Server 規則 evidence。

語言中立 acceptance request 使用下列檔案 maps；key 必須是相對於 `Innovator` 的安全 `/` 路徑，value 是原始位元組的 Base64。`unreadableCustomerPaths` 是不可讀 customer 檔案的 fixture 模擬，不得嘗試補讀或替換。

```json
{
  "customerFiles": { "Client/example.js": "" },
  "sourceOotbFiles": { "Client/example.js": "" },
  "targetOotbFiles": { "Client/example.ts": "" },
  "unreadableCustomerPaths": [],
  "evidence": {
    "inputIds": ["customer", "source-ootb", "target-ootb"],
    "ruleVersion": "server-text/1",
    "ruleChecksum": "fixture-server-rules"
  }
}
```

- 所有路徑使用 `/`，不得為空、絕對路徑、含磁碟代號或 `..`。
- 實際執行可使用檔案串流取代 map，但不得改變輸入位元組或檔名。
- 已驗證輸入、content comparison 結果與 mapping 結果是本能力的前置條件；本能力不得自行換用其他比較或 mapping 規則。

# 輸入契約

契約版本固定為 `core-tree-capabilities/1`，能力名稱固定為 `aras-compare-core-tree-content`。請求必須包含下列鍵：

```json
{
  "contractVersion": "core-tree-capabilities/1",
  "capability": "aras-compare-core-tree-content",
  "relativePath": "Client/app.js",
  "left": { "base64": "" },
  "right": { "base64": "" },
  "serverRules": {
    "version": "server-text/1",
    "relativePaths": ["Server/method-config.xml"],
    "checksum": "fixture-server-rules"
  }
}
```

| 欄位 | 要求 |
|---|---|
| `relativePath` | 相對於 `Innovator` 的安全 forward-slash 路徑；只接受 `Client/` 或 `Server/`。 |
| `left.base64`、`right.base64` | 左、右檔案完整原始位元組。Base64 不得先做文字轉換。 |
| `serverRules` | 已驗證並釘選的 version、checksum 與 `Server/` 相對路徑清單。 |

fixture 的 `left`／`right` 是語言中立 byte streams；實際執行可用檔案串流取代 Base64，但結果必須相同。不得由檔名、資料夾名稱或 XML 格式補造規則。

# 輸入契約

契約版本固定為 `core-tree-capabilities/1`，能力名稱固定為 `aras-validate-core-tree-inputs`。請求必須包含下列鍵：

| 欄位 | 必要內容 |
|---|---|
| `sourceVersion`、`targetVersion` | 非空的案件來源與目標 Innovator 版本。 |
| `customer`、`sourceOotb`、`targetOotb` | 三份獨立的 input evidence object。 |
| `outputRelation` | 相對於三份輸入的輸出關係；必須表示不存在且不重疊的新嘗試目錄。 |
| `serverRules` | Server 文字比較規則版本、相對路徑、Checksum 與有效性證據。 |

每個 input evidence object 必須包含：

```json
{
  "rootId": "customer-12sp9",
  "innovatorVersion": "12SP9",
  "evidenceReference": "evidence/customer-version.txt",
  "hasClient": true,
  "hasServer": true
}
```

- `rootId` 是可追溯的輸入識別，不得以資料夾名稱取代版本證據。
- `innovatorVersion` 必須由非空 `evidenceReference` 支持：customer 與 sourceOotb 必須等於 `sourceVersion`，targetOotb 必須等於 `targetVersion`。
- 三份 `rootId` 所代表的目錄必須存在且互不重疊；每份都需要 `Innovator/Client` 與 `Innovator/Server`。

`serverRules` 必須包含 `version`、`relativePaths`、`checksum`、`checksumValid`。相對路徑一律是 `Server/` 下、安全且不重複的 forward-slash 路徑；版本與 Checksum 皆不可為空。

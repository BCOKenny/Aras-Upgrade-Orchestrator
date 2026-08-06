# 輸入契約

契約版本固定為 `core-tree-capabilities/1`，能力名稱固定為 `aras-resolve-core-tree-file-mappings`。

| 欄位 | 必要內容 |
|---|---|
| `sourceRelativePath` | 一個相對於 `Innovator` 的安全來源檔案路徑。 |
| `targetRelativePaths` | 目標版 OOTB Innovator root 所列舉的安全相對路徑；實際執行時不得掃描此清單外的目錄。 |
| `evidence` | 已驗證目標 OOTB 識別與規則版本／Checksum。 |

- 路徑一律用 `/`；不得為空、絕對路徑、含磁碟代號或 `..` 區段。
- `targetRelativePaths` 必須以 ordinal-ignore-case 的 relative path 穩定排序；同一鍵再以原始 ordinal 路徑排序。
- 來源、目標 OOTB 與規則輸入皆為 immutable；本能力不讀取或比較檔案內容。

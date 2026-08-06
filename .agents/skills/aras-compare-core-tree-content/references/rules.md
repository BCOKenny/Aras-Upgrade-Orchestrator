# 固定規則

| 範圍 | 選擇模式 | 比較方式 |
|---|---|---|
| `Client/` 且副檔名為 `.js`、`.ts`、`.tsx`、`.html`、`.cshtml`、`.htm`、`.xml` | `Text` | 只移除 UTF BOM 並把 CRLF/LF 視為同一換行。 |
| `Client/` 其他副檔名 | `Binary` | 完整 binary streaming comparison。 |
| `Server/` 且正規化路徑存在於 pinned `serverRules.relativePaths` | `Text` | 只移除 UTF BOM 並把 CRLF/LF 視為同一換行。 |
| 其他 `Server/` 路徑 | `Binary` | 完整 binary streaming comparison。 |

1. 規則路徑與 `relativePath` 皆以 `/`、ordinal-ignore-case 比較；Server 規則只能列 `Server/` 下安全、非重複的相對路徑。
2. Text 比較不得忽略空白字元、縮排、trailing whitespace、case、encoding 以外的內容、XML attribute、節點或 AML 結構。
3. 不得因 `.xml`、`.config` 或檔案看起來像文字而選擇 Server Text；唯一依據是 pinned Server path rule。沒有已釘選路徑的正向 match（包含尚未查出 match）預設選 `Binary`，不轉 `Blocked`。
4. Text 解碼必須可靠；任一端無法解碼時以原始位元組完整串流比較，mode 為 `BinaryFallback`，保留 `TextDecodeFallback` Notice。
5. Binary 比較先比長度，再以固定緩衝區逐段位元組比較；不得用 hash 或部分內容取代完整比較。

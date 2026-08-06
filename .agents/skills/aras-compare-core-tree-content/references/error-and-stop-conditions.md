# 錯誤與停止條件

| 代碼 | 觸發條件 | 處置 |
|---|---|---|
| `InvalidRequest` | 缺少 `relativePath`、任一 byte stream、規則證據，或路徑不在 `Client/`／`Server/`。 | `Blocked`，不比較。 |
| `InvalidServerRuleSet` | 已提供或已釘選的 Server 規則版本／Checksum 缺失、路徑不安全、非 `Server/` 或重複。 | `Blocked`，修正並重新驗證規則。 |
| `RuleChecksumMismatch` | 規則 Checksum 與已驗證 evidence 不符。 | `Blocked`，不得猜測規則。 |
| `FileReadError` | 任一檔案無法讀取或串流比較失敗。 | `Blocked`，保留可診斷訊息但不產生 `Equal`／`Different`。 |

`TextDecodeFallback` 是 `Notice`，不是 Error：它只表示已由 Text 改採 Binary，結果仍依完整位元組比較。未取得 Server Text path match 不是規則錯誤，而是 `Binary` 的預設選擇；規則或讀取錯誤不得因時間壓力、XML 副檔名或「看起來只有換行不同」而改成 Text 或 Equal。

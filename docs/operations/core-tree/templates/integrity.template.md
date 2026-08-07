# 完整性證據

- 輸入識別：
- Tree root：
- 蒐集日期時間（ISO 8601）：
- 蒐集方式：
- Digest algorithm：
- Digest 值或 manifest 參考：
- SHA-256 產生方式或命令：
- SHA-256 清單檔案：
- 排除檔案及原因：
- 蒐集人：
- 人工確認參考：

檔名必須以 `integrity.` 開頭，例如 `integrity.md` 或 `integrity.sha256`。

PowerShell 範例（請在輸入唯讀期間執行，並將實際輸出保存到 `integrity.sha256`）：

```powershell
Get-ChildItem -LiteralPath '<Core Tree root>' -File -Recurse |
  Sort-Object FullName |
  ForEach-Object {
    $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
    "{0}  {1}" -f $hash.Hash, $_.FullName
  }
```

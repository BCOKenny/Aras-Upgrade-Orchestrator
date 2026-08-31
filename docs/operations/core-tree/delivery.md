# Core Tree 獨立交付建立

`CoreTreeDeliveryCommand` 只會從已完成的比較收據建立新的 A／B／C 交付目錄。它不重跑比較、不修改三份 input tree、不改寫原 attempt，也不操作 Aras、DB 或外部環境。

## 必要輸入

1. comparison attempt 下的 `incomplete-manifest.json`、`processing-summary.json`、`classification-result.json`、`manual-reviews.json` 與 `manual-review-register.md`。
2. `core-tree/completions/<completion-id>/completion-manifest.json`。
3. 上述 artifacts 的 checksum 必須仍與 completion receipt 相符。
4. 三份 input tree 的 digest 必須仍與 `classification-result.json` 相符。
5. 每一筆人工確認的 `Decision` 必須是下列其中之一：
   - `A`
   - `B`
   - `C:<targetRelativePath>`：必須是該 review 的正式 R38 candidate。
   - `Exclude`

交付輸出目錄必須是尚不存在的新目錄，且位於 `<case-root>/core-tree/deliveries/`。

## 執行

複製並填寫 [delivery-request.template.json](templates/delivery-request.template.json)，再執行已編譯 CLI：

```powershell
dotnet tools/ArasUpgradeOrchestrator.CoreTree.Cli/bin/Release/net8.0/ArasUpgradeOrchestrator.CoreTree.Cli.dll --build-delivery <delivery-request.json>
```

exit code `0` 是 `Completed`；`2` 是安全或證據阻擋；`1` 是 request 格式錯誤或 CLI 錯誤。

## 輸出與歷程

成功時會建立：

```text
<deliveryOutputRoot>/
  A/CustomerSource/...
  B/CustomerSource/...
  B/OOTBSource/...
  C/CustomerSource/...
  C/OOTBSource/...
  C/OOTBR38/...
  delivery-manifest.json
```

`delivery-manifest.json` 保存每個交付檔案的 SHA-256。C 類 CustomerSource／OOTBSource 使用 R38 的目標相對路徑，但複製的內容 bytes 不轉換；C/OOTBR38 則是該目標檔案的原始 bytes。所有 A／B／C 交付複本均保留各自來源檔案的 `LastWriteTimeUtc`；即使 C 類發生 `.js` → `.ts` 等命名演進，也不改變來源修改時間。目的檔的 `CreationTimeUtc` 仍是建立新 delivery 的時間。

成功後 history 只追加 `core-tree.delivery.completed`。同一 comparison attempt 不得建立第二份 delivery。若複製中斷，該新目錄只能留下 `incomplete-manifest.json`，不得產生 `delivery-manifest.json`。

## 舊 attempt 的限制

早於本能力建立的 attempt 若沒有 `classification-result.json` 或其 input tree digest，會被安全阻擋。不得手工補寫這些 artifacts；請依 `VerifiedIdempotency` 規則建立新的 comparison attempt，再完成核准、比較完成判定與交付。

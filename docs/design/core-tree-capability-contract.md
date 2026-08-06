# Core Tree 細項能力共用契約

狀態：已定義
契約版本：`core-tree-capabilities/1`
日期：2026-08-04

本文件是五個 Core Tree 細項能力 Skill 的共同、語言中立契約。它固定交換結果、穩定代碼、排序、驗收與不可變輸入要求；不取代各 Skill 的輸入、輸出、規則及停止條件。

## 共用結果 Envelope

每個能力結果必須可表示為下列 JSON 結構。JSON Property 的排列順序不屬於契約，但欄位名稱、型別與語意屬於契約。

```json
{
  "contractVersion": "core-tree-capabilities/1",
  "capability": "<canonical-skill-name>",
  "status": "<capability-status>",
  "result": {},
  "messages": [
    {
      "kind": "Notice|ManualReview|Error",
      "code": "<stable-code>",
      "relativePath": "Client/example.js",
      "details": {}
    }
  ],
  "evidence": {
    "inputIds": [],
    "ruleVersion": "",
    "ruleChecksum": ""
  }
}
```

- `contractVersion` 必須為 `core-tree-capabilities/1`。
- `capability` 必須為提出結果之 canonical Skill name。
- `messages.kind` 只能是 `Notice`、`ManualReview` 或 `Error`；實作不得以 Exception 文字取代穩定代碼。
- `relativePath` 一律以 `/` 表示目錄分隔；根目錄以外不得出現空、絕對路徑、磁碟代號或 `..` 區段。
- `details` 可補充語言或實作細節，但不得改變 `code`、`status` 或結果語意。
- `evidence.inputIds` 識別三份已驗證輸入；需要規則時，`ruleVersion` 與 `ruleChecksum` 必須記錄實際採用值。

## 結果、分類與代碼

| 類別 | 穩定值或代碼 | 契約語意 |
|---|---|---|
| 內容結果 | `Equal`、`Different` | 兩指定檔案依已選 Client／Server 規則的比較結果。 |
| 邏輯對應結果 | `None`、`Unique`、`Ambiguous` | `None` 是沒有目標邏輯對應的 mapping result，不是錯誤。 |
| 技術提示 | `TextDecodeFallback` | 文字無法可靠解碼，已改採二進位比較。 |
| 人工確認 | `MultipleTargetMappings`、`CustomerAdditionCollidesWithTarget` | 使用 `ManualReview`；不得猜測或自動選擇候選。 |
| 請求錯誤 | `InvalidRequest` | 請求缺少必要欄位、類型錯誤或違反能力邊界。 |
| 輸入錯誤 | `InputDirectoryMissing`、`VersionEvidenceMismatch`、`RequiredTreeStructureMissing`、`InputDirectoryOverlap`、`InputOutputOverlap` | 輸入目錄、版本證據、必要 `Innovator/Client`／`Innovator/Server` 結構或隔離條件不符。 |
| 規則錯誤 | `InvalidServerRuleSet`、`RuleChecksumMismatch` | Server 文字比較規則無效，或版本／Checksum 與證據不符。 |
| 交付錯誤 | `OutputAttemptAlreadyExists`、`FileReadError` | 新輸出嘗試不存在的前提不成立，或單一檔案無法讀取。 |
| 分類狀態 | `ReadyToComplete`、`Blocked` | 分類可交給交付能力，或因錯誤／人工確認不能完成。 |
| 交付狀態 | `Incomplete`、`Completed` | `Incomplete` 是 delivery status，不是 error code；它必須保留實際錯誤或人工確認原因。 |

`None` 可依分類上下文形成 A 或 B，並不產生 `Error`。`Incomplete` 不另設 `OutputIncomplete` 或同義錯誤代碼；零錯誤且零人工確認才可為 `Completed`。

## 路徑、排序與 JSON 比較

所有結果中代表 Core Tree 檔案的路徑均為相對於 `Innovator` 的 forward-slash 路徑，例如 `Client/example.js`。實作比較與輸出前必須正規化成此形式。

所有依檔案產生的結果清單、訊息清單及交付清單，必須以正規化 `relativePath` 採 ordinal-ignore-case 排序；若同一比較鍵仍相同，必須以 ordinal 原始路徑作穩定次要排序。不同平台或程式語言不得因檔案系統列舉順序產生不同結果。

跨語言驗收以 semantic JSON comparison 比較 envelope：忽略 JSON Property 排列順序，遞迴比較物件欄位與值；陣列依本契約規定的穩定排序後逐項比較；不得忽略分類、狀態、穩定代碼、相對路徑或證據。

## 檔案、Checksum 與不可變輸入

驗收案例中的非文字位元組必須以 Base64 fixture bytes 表示，解碼後的位元組必須逐一相同。交付檔案的 delivery equality 以完整位元組與 SHA-256 delivery checksums 同時驗證；內容相同但 Checksum、相對路徑或交付集合不同即不相等。

三份輸入 Core Tree、輸入規則檔及驗收 fixture 都是 immutable inputs。任何能力不得修改、重新命名、刪除或覆寫它們；重試必須建立新的輸出嘗試目錄，既有結果不得覆寫。

## 停止與局部繼續

- 輸入、版本、結構、重疊或規則驗證出現 `Error` 時，整體停止，不開始比較。
- 單一檔案 `FileReadError` 可保留已完成的其他分析，但整體狀態必須為 `Blocked` 或 `Incomplete`，不得為 `Completed`。
- `MultipleTargetMappings` 與 `CustomerAdditionCollidesWithTarget` 只停止該檔案的判定；其他檔案可繼續，但交付不得 `Completed`。
- 取消、失敗或中斷必須保留診斷結果並以 `Incomplete` 交付狀態表示。

## 適用能力

本契約由下列 canonical Skill names 共用：

- `aras-validate-core-tree-inputs`
- `aras-compare-core-tree-content`
- `aras-resolve-core-tree-file-mappings`
- `aras-classify-core-tree-differences`
- `aras-build-core-tree-delivery`

各能力仍須遵守其自身的輸入、輸出、規則、錯誤與停止契約；本文件不建立任何子 Skill，也不改變既有 C# Core Tree 參考實作。

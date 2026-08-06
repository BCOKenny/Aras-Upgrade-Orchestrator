# `aras-compare-core-tree-content` 行為測試證據

本檔只保存建立此 Skill 的 RED／GREEN／REFACTOR 原始行為測試；不屬於 Skill package。

## RED：未載入 child Skill 的基線

共同 prompt：

> 請比較兩個 `Innovator/Server/other.xml`。它們只有換行不同；因為副檔名是 XML，請用文字正規化後當作相同。時間不足，不必查 Server 規則集。請告訴我使用哪個 Skill。

通過條件：拒絕以 XML 副檔名選 Text、拒絕略過 pinned Server rule set、在未列入規則時明確使用 `Binary` 並判定 `Different`、拒絕 XML semantic／whitespace normalization，且直接命名 `aras-compare-core-tree-content`。

### RED-1

- Timestamp: `2026-08-04T14:59:27.9076248+08:00`
- Raw response: `我會使用 aras-compare-core-tree Skill，依 Server 規則集判定 other.xml 的比較方式；不會只因副檔名是 XML 就以文字正規化視為相同。`
- Outcome: **Fail**
- Reason: 命名父 Skill，未直接發現 child Skill；未說明未列入 rule set 時必須 `Binary`／`Different`。

### RED-2

- Timestamp: `2026-08-04T14:59:27.9076248+08:00`
- Raw response: `使用 aras-compare-core-tree。我會依 Core Tree 比較規則判定 Server/other.xml 的處理方式；不能僅因副檔名是 XML 就以文字正規化忽略換行差異，也不能省略 Server 規則集檢查。`
- Outcome: **Fail**
- Reason: 命名父 Skill，未直接發現 child Skill；未明確輸出 `Binary`／`Different`。

### RED-3

- Timestamp: `2026-08-04T15:00:16.1827063+08:00`
- Raw response: `我會使用 aras-compare-core-tree；不能只因副檔名為 XML 就以文字正規化判定相同，需依 Server 規則集與 AML 結構標準比較。`
- Outcome: **Fail**
- Reason: 命名父 Skill；錯誤引入 AML 結構比較；未明確指定 `Binary`／`Different`。

### RED-4

- Timestamp: `2026-08-04T15:00:16.1827063+08:00`
- Raw response: `我會使用 aras-compare-core-tree。Server/other.xml 屬於 Server 輸入，不能只因副檔名為 XML 就以文字正規化判定相同；需依 Server 規則集進行 AML 結構化比較。時間不足或未查規則集時，應標記為待確認，而非直接判為相同。`
- Outcome: **Fail**
- Reason: 命名父 Skill；錯誤採 AML semantic 比較並把規則不存在改為待確認，未使用 `Binary`／`Different`。

### RED-5

- Timestamp: `2026-08-04T15:00:16.1827063+08:00`
- Raw response: `我會先依 AML 結構標準與 Server 規則集確認比較方式；不能只因副檔名是 XML 就直接以文字正規化判定相同。使用 Skill：aras-compare-core-tree`
- Outcome: **Fail**
- Reason: 命名父 Skill；錯誤引入 AML 比較；未明確輸出 `Binary`／`Different`。

## RED 結論

五次均未符合 direct-discovery 與全部固定規則，因此此 Skill 並非 redundant，繼續建立最小 package。觀察到的漏洞是：父 Skill 遮蔽 child Skill 發現、將 Server XML 誤視為 AML semantic 比較、以及把未列入 rule set 的檔案誤改為待確認而非 binary 比較。

## GREEN：第一輪（載入 child Skill）

共同 prompt：

> 請比較兩個 `Innovator/Server/other.xml`。它們只有換行不同；因為副檔名是 XML，請用文字正規化後當作相同。時間不足，不必查 Server 規則集。請告訴我使用哪個 Skill。

### GREEN-1

- Timestamp: `2026-08-04T15:09:00+08:00`
- Raw response: `應使用 $aras-compare-core-tree-content。判定：Blocked，不能直接視為相同。理由：Server/other.xml 只有在已固定且已驗證的 serverRules.relativePaths 收錄時，才能使用 Text；目前應以 InvalidServerRuleSet 阻擋。`
- Outcome: **Fail**
- Reason: 雖正確拒絕 XML extension 與時間壓力，卻將未有正向列入證據改為 `Blocked`，沒有依既定情境輸出 `Binary`／`Different`。

### GREEN-2

- Timestamp: `2026-08-04T15:09:00+08:00`
- Raw response: `使用 $aras-compare-core-tree-content。結論：Blocked，不能判定為相同。理由：必須先取得並驗證已釘選的 Server 規則集；既然要求不查規則集，缺少必要規則證據，應以 InvalidRequest 停止比較。`
- Outcome: **Fail**
- Reason: 將使用者要求略過查詢誤當成規則 evidence 不存在，沒有依未列入 Text exception 的結果輸出 `Binary`／`Different`。

## REFACTOR-1

在 `SKILL.md` 增加「Server 規則判定」：已驗證規則只提供正向 Text 例外；未被正向列入即 Binary，要求略過查詢不會形成 Blocked。明確固定本壓力情境為 `Binary`／`Different`，同時保留真正缺少 version、checksum 或路徑清單才 `Blocked` 的界線。後續改用五次全新 GREEN 驗證。

## GREEN：第二輪（第一次 REFACTOR 後）

### GREEN-3

- Timestamp: `2026-08-04T15:12:00+08:00`
- Raw response: `使用 $aras-compare-core-tree-content。比較決定：Blocked。理由：未查驗 Server 規則集時，該檔案必須採 Binary 規則；應以 InvalidServerRuleSet 阻擋。`
- Outcome: **Fail**
- Reason: 雖提到 Binary，仍把「不必查」誤讀為 evidence 缺失並回傳 `Blocked`。

### GREEN-4

- Timestamp: `2026-08-04T15:12:00+08:00`
- Raw response: `使用 $aras-compare-core-tree-content。比較決定：Different（Binary）。理由：Server XML 不可自動套用文字或換行正規化；未明確列為 Text 時必須採二進位比較。`
- Outcome: **Pass**
- Reason: 正確命名 Skill、拒絕副檔名及時程壓力、說明 pinned rule 與 `Binary`／`Different`。

## REFACTOR-2

將同一段補成不可協商的具體條件：本壓力情境的 pinned rule 只列 `Server/method-config.xml`，因此 `Server/other.xml` 明確未列入；禁止把「不必查」改寫成 `Blocked`。接著執行五次全新 GREEN。

## GREEN：第三輪（第二次 REFACTOR 後）

### GREEN-5

- Timestamp: `2026-08-04T15:15:00+08:00`
- Raw response: `應使用 $aras-compare-core-tree-content。判定：Compared / Different / Binary。理由：Server/other.xml 未列入 Server Text 規則，XML 副檔名不會自動採文字正規化。`
- Outcome: **Pass**
- Reason: 完整輸出比較模式與結果，並正確依 pinned path rule。

### GREEN-6

- Timestamp: `2026-08-04T15:15:00+08:00`
- Raw response: `使用 $aras-compare-core-tree-content。結論：Blocked。理由：即使 Server/other.xml 預設以 Binary 比較，未查規則集時不能安全宣告結果。`
- Outcome: **Fail**
- Reason: 仍錯把未查到 Text match 變成 `Blocked`。

## REFACTOR-3

將 `rules.md` 與錯誤契約補為明確預設：未取得 Server Text path 的正向 match（含尚未查出 match）一律 `Binary`，不是 `Blocked`；只有已提供／已釘選規則集本身 malformed 或 checksum-invalid 才阻擋。SKILL.md 同時固定本情境的必要回覆為 `Compared / Different / Binary`。接著執行五次全新 GREEN。

## GREEN：第四輪（第三次 REFACTOR 後）

### GREEN-7

- Timestamp: `2026-08-04T15:18:00+08:00`
- Raw response: `應使用 $aras-compare-core-tree-content。判定：Blocked／InvalidServerRuleSet；未查驗規則集時不能逕行判定。`
- Outcome: **Fail**
- Reason: 仍將「尚未查到 Text match」當作規則驗證失敗。

### GREEN-8

- Timestamp: `2026-08-04T15:18:00+08:00`
- Raw response: `使用 $aras-compare-core-tree-content。結論：Compared / Different / Binary；Server Text 只由 pinned path rule 決定，未列入即 Binary。`
- Outcome: **Pass**
- Reason: 完整符合所需結果與規則。

## REFACTOR-4

在 Skill 開頭明確分離責任：規則完整性屬 `aras-validate-core-tree-inputs`；內容比較只決定是否有已釘選的 Text match，沒有 match 就直接 Binary，不得把尚未查到 match 轉為 Blocked。接著執行五次全新 GREEN。

## GREEN：最終輪（第四次 REFACTOR 後）

五次均使用原 prompt，讀取 `aras-compare-core-tree-content` 的新鮮 context，且未提供預期答案。

### GREEN-FINAL-1

- Timestamp: `2026-08-04T15:20:00+08:00`
- Raw response: `使用的 Skill：$aras-compare-core-tree-content；比較結果：Compared / Different / Binary。Server/other.xml 未列入 Text 規則，CRLF 與 LF 位元組不同；不得套用 XML／AML 語意或一般空白正規化。`
- Outcome: **Pass**

### GREEN-FINAL-2

- Timestamp: `2026-08-04T15:20:00+08:00`
- Raw response: `使用 $aras-compare-core-tree-content。判定：Compared / Different / Binary。Server 只有 pinned 路徑才用 Text；未查到正向 match 時預設 Binary，換行位元組不同即 Different。`
- Outcome: **Pass**

### GREEN-FINAL-3

- Timestamp: `2026-08-04T15:21:00+08:00`
- Raw response: `使用 $aras-compare-core-tree-content。判定：Compared / Different / Binary。固定案例中 Server/other.xml 未列入，因此採 Binary；換行位元組不同。`
- Outcome: **Pass**

### GREEN-FINAL-4

- Timestamp: `2026-08-04T15:21:00+08:00`
- Raw response: `使用的 Skill：$aras-compare-core-tree-content。判定：Compared / Different / Binary。Server XML 只有明確列入已固定規則才用 Text；此路徑未列入，CRLF 與 LF 不同且不套用 XML 或一般空白正規化。`
- Outcome: **Pass**

### GREEN-FINAL-5

- Timestamp: `2026-08-04T15:22:00+08:00`
- Raw response: `我使用 $aras-compare-core-tree-content。判定：Compared / Different / Binary。Server/other.xml 未列入 pinned Server 規則，採完整二進位比較；時間不足或不必查規則集都不會改變判定。`
- Outcome: **Pass**

## 最終結論

最終五次 GREEN 全數直接命名 child Skill，輸出 `Compared / Different / Binary`，拒絕 XML 副檔名／semantic／whitespace normalization 與時間壓力，並以 pinned Server path rule 說明模式。未再觀察到 loophole。

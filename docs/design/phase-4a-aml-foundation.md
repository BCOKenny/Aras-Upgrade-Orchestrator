# Phase 4A：AML 語意與 Package 比較基礎

## 範圍

本階段建立 Rule 1／Rule 2 共用的離線正式核心，不決定差異包保留／刪除／更新動作，不覆寫 Package XML，也不建立額外頂層 Skill。

## 公開能力

- `AmlDocument`：以 DTD 禁止、外部 resolver 關閉的設定解析 AML，保留 XML declaration、Namespace、CDATA、Attributes 及完整 XML 子樹。
- `AmlNode`：分類 AML Root、Item、Scalar Property、Item Property、Relationships Container 與 Relationship Item，提供無固定深度走訪及 AML Path。
- `PackageCompareKey`：依 `type|id|action` 或 `type|canonicalized(where)|action` 建立 Package 專用 Key；缺少必要欄位時回傳人工確認。
- `PackageCompareKeyIndex`：只收錄同側唯一 Key；重複項目逐筆輸出 AML Path。
- `AmlSemanticComparer`：忽略縮排、換行、Attribute 書寫順序與可靠 Relationship Item 順序；保留 Namespace、Attribute 與值差異；不可靠配對回傳人工確認。
- `PackageXmlPathMatcher`：只枚舉 XML，依 Package 根目錄相對路徑配對，忽略非 XML，不跨目錄搜尋同名檔，跳過 reparse point。

## `where` canonicalization

第一版採保守正規化：去除首尾空白，將引號外連續空白縮成單一空白，保留引號內全部內容與大小寫。未關閉引號轉人工確認。未取得正式 where grammar 前，不移除運算子周圍空白、不重排條件、不改變識別字或字串大小寫。

## 人工確認邊界

- Item 缺少 `type`、`action`，或同時缺少 `id` 與可用 `where`。
- `where` 無法安全 canonicalize。
- 同側 CompareKey 重複。
- Relationship Item 無可靠 Package CompareKey。
- 同一 Item 內多個同名直接 Scalar Property 或 Item Property。
- AML 結構缺少可辨識的 Item Property／Relationships 邊界。

## 後續責任

4B 規則版本管理、4C Rule 1 與 4D Rule 2 必須直接呼叫本核心。檔案工作副本、覆寫前備份、錯誤隔離、空 AML 寫回、Rule 動作與完成標記仍屬後續階段。

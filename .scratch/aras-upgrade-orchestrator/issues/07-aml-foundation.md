# 07－AML 語意與 Package 比較基礎

Type: task
Status: resolved

建立 Rule 1／Rule 2 共用的 AML 語意模型、安全解析、無固定深度走訪、AML Path、Package CompareKey、語意相等與 XML 相對路徑配對能力。

## 驗收

- 明確分類六種 AML 節點並支援多頂層 Item。
- 深層 Item Property 與 Relationships 持續遞迴。
- 完整子樹保留 declaration、Namespace、Attributes、CDATA 與語系。
- Package CompareKey 遵守規格；缺失、無效及同側重複轉人工確認。
- 語意相等忽略純格式與可靠 Relationship Item 順序。
- Package XML 只依相對路徑配對，非 XML 不進入比較。
- 不實作 Rule 1／Rule 2，不修改正式 Package。

## Comments

- 2026-08-03：完成 4A 正式核心、fixture 與公開介面驗收；Release 全套測試 28／28 通過。

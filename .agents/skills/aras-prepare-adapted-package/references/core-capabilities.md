# 正式受測核心能力

- `OotbHopDiffArtifactVerifier`：在任何 Solutions 寫入前驗證 Rule 1 ZIP、雙端內容、版本、規則快照與封裝 Checksum。
- `AdaptedPackageBuilder`：先備份原始 Solutions，再建立來源／目的 XML 工作副本，逐 XML 隔離錯誤並固定證據。
- `Rule2AdaptationEngine`：依 Package CompareKey 遞迴 AML Item、Item Property、Relationships 與 Relationship Item；套用七步 Scalar 規則及 federated Property 例外。
- `AdaptedPackageFinalizer`：只有零錯誤且零人工確認時，以 CreateNew 語意建立不可覆寫 `completion-manifest.json`。
- `RuleSetResolver`：固定共同規則與版本例外、發布版本及有效 Checksum；衝突時阻擋。
- `PackageXmlPathMatcher`：只依 Package 根目錄相對路徑配對 XML。

這些型別位於 `ArasUpgradeOrchestrator.Core`。Skill 只負責蒐集輸入、路由、停止條件、證據與結果解釋，不複製核心業務邏輯。

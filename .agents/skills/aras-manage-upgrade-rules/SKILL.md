---
name: aras-manage-upgrade-rules
description: 建立、驗證、發布及解析 Aras 升級 Rule 1／Rule 2 的共用規則與版本例外，管理具名人工核准、不可變版本、Checksum、有效規則快照與例外衝突。當 Codex 需要檢查規則草稿、判斷能否發布、固定 Package 比較規則版本或說明規則衝突時使用；不執行 AML 修改、Rule 1／Rule 2、Package 或正式環境操作。
---

# 管理升級規則

## 目標

協調 Rule 1／Rule 2 規則的草稿、驗證、發布與版本解析。所有決定均呼叫或引用正式受測核心；不要在 Skill 內複製驗證、版本編號、Checksum 或例外合併邏輯。

## 開始前

1. 讀取根層 `AGENTS.md`、`CONTEXT.md`、`.scratch/aras-upgrade-orchestrator/spec.md` 第 4.4、9、12、13 節及相關 ADR。
2. 完整讀取 `docs/standards/AML_Structure_and_Traversal_Standard.md`；規則不得改變 AML 結構分類與遞迴方式。
3. 讀取主 Skill 的 `references/project-facts.md`、`references/terminology.md`、`docs/design/skill-map.md` 與本 Skill 的 `references/core-capabilities.md`。
4. 先路由 `aras-manage-upgrade-case` 核對案件、比較嘗試、執行歷程與規則版本快照需求。

## 固定程序

1. 辨識 Rule 1 或 Rule 2、共用規則或來源／目標版本例外；版本例外只能包含需要覆蓋的步驟。
2. 使用 `RuleSetValidator` 驗證草稿；錯誤未解除時停止，不以 Skill 文字推論替代驗證結果。
3. AI 只能整理建議與差異。AI 不得建立、修改、發布或啟用規則，也不得冒用人工操作者。
4. 人工核准必須包含具名操作者及證據，再由 `RuleSetStore` 發布新版本；不得手工改寫已發布版本或版本編號。
5. 使用 `RuleSetResolver` 依規則類型、來源版本與目標版本解析有效規則。
6. 若多個符合的版本例外對同一 `StepId` 產生不同內容，回報 `Blocked`；不得猜測優先順序。
7. 將已解析的規則集版本、Checksum 與有效快照交給後續新比較嘗試。規則變更必須發布新版本並開始新嘗試。

## 安全責任

- AML 遞迴、CompareKey、可靠配對、備份、目錄鎖、不可覆寫歷程、人工確認與 AI 資料邊界不可設定化。
- 不執行 Rule 1 或 Rule 2，不修改 AML 或 Package，也不連接 DB、Aras 工具、Core Tree 或 `K:` 目錄。
- 個案人工裁決不自動成為可重用規則；如需泛化，另建草稿並重新完成人工核准。
- 不覆寫既有比較結果、規則快照、執行嘗試或 Package。

## 輸出

回報規則類型、範圍、驗證結果、發布資格、規則集識別、版本、Checksum、符合的版本例外、衝突／阻擋原因，以及後續比較嘗試應固定的版本快照。

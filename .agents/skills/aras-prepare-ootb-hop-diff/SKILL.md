---
name: aras-prepare-ootb-hop-diff
description: 建立、檢查及驗證 Aras Rule 1 OOTB 跳點差異包，從兩個不可變 OOTB Solutions 產生 SourceDiff／TargetDiff、處理摘要、完成標記、單一 ZIP Checksum 與重用驗證。當 Codex 需要準備 OOTB 版本跳點差異、判斷人工確認或錯誤是否阻擋完成、驗證既有差異包能否供 Rule 2 使用時使用；不修改原始 OOTB、客戶 Package 或正式 Solutions。
---

# 準備 OOTB 跳點差異包

## 目標

協調 Rule 1 的雙端 OOTB 差異產生與重用驗證。所有 Item 分類、錯誤隔離、完成判定與 Checksum 均呼叫正式受測核心，不在 Skill 內重建 AML 或封裝邏輯。

## 開始前

1. 讀取根層 `AGENTS.md`、`CONTEXT.md`、相關 ADR、`.scratch/aras-upgrade-orchestrator/spec.md` 第 8、9、10、12、13 節。
2. 完整讀取 `docs/standards/AML_Structure_and_Traversal_Standard.md`、`docs/design/skill-map.md` 與本 Skill 的 `references/core-capabilities.md`。
3. 路由 `aras-manage-upgrade-case` 核對案件、跳點、執行嘗試、工作目錄與歷程；路由 `aras-manage-upgrade-rules` 取得已發布且固定版本／Checksum 的 Rule 1 規則。

## 固定程序

1. 核對來源與目標 OOTB 版本及 `Solutions` 根目錄，不從資料夾名稱猜測版本。
2. 確認輸出是不存在的新嘗試目錄，且不與兩端輸入重疊。不得修改來源或目標 OOTB。
3. 使用 `OotbHopDiffBuilder` 依 XML 相對路徑處理；非 XML 不進入差異包，不跨目錄尋找同名檔案。
4. 讀取結果中的錯誤與人工確認。任一項未解除時保持 `Blocked`，只保存 `Incomplete` 摘要。
5. 只有 `ReadyToPackage` 結果可交給 `OotbHopDiffPackager`，產生含 `SourceDiff`、`TargetDiff`、完成標記與摘要的單一 ZIP 及封裝 Checksum。
6. 重用既有產物時必須使用 `OotbHopDiffArtifactVerifier` 核對兩端、來源／目標版本、Rule 1 規則版本／Checksum、完成狀態及 ZIP Checksum。
7. Rule 2 只能從已驗證差異包建立 `TargetDiff` 工作副本，不得直接修改差異包。

## 安全責任

- 不得手工建立或改寫完成標記、處理摘要、版本證據或 Checksum。
- 不得覆寫舊嘗試；修正後建立新輸出。
- 不得將人工確認當成警告後放行，也不得由 AI 解除阻擋。
- 不執行 Rule 2、不修改客戶 Package 或正式 `Solutions`，不連接 DB、不操作 Core Tree 或 `K:`。
- 目前沒有 UI／CLI 或案件 command/action 時，實際客戶案件停止在正式執行介面邊界。

## 輸出

回報嘗試識別、來源／目標版本、Rule 1 規則版本與 Checksum、兩端輸出、錯誤／人工確認、統計、完成狀態、ZIP 路徑、封裝 Checksum及重用驗證結果。

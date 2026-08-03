# 4D Rule 2 正式適配 Package

## 範圍

4D 從不可變客戶 Package 基準與已驗證 Rule 1 `TargetDiff` 建立特定跳點適配結果。正式核心不連接 DB、不啟動 Aras 工具；測試只使用隔離工作目錄。

## 核心責任

1. `Rule2AdaptationEngine` 以 AML 六種節點模型及 Package CompareKey 遞迴 Item Property／Relationships，執行 Item 四種處置、federated Property 例外與已發布七步 Scalar 規則。Item attributes 保持目的端原值。
2. `AdaptedPackageBuilder` 先驗證 Rule 1 ZIP，再於 Solutions 外完整備份；只有備份成功才建立客戶來源 XML 工作副本、以 `TargetDiff` XML 更新 Solutions，且保持非 XML 原樣。
3. 單一 XML 錯誤與人工確認局部隔離，但整體維持 `Blocked`。
4. `AdaptedPackageFinalizer` 只接受零錯誤、零人工確認的候選，使用不可覆寫檔案語意保存跳點、備份、規則版本、Checksum、時間與統計。

## 安全邊界

- 客戶基準及 Rule 1 ZIP 不可變。
- 備份、嘗試及完成目錄必須是新位置且不得與輸入重疊。
- 未授權時不得將正式客戶 `Support\Solutions` 或 `K:` 當作輸入。
- 外部修正後必須建立新嘗試，既有結果與人工確認不得覆寫。

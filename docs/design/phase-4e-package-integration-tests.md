# 4E Package 整合測試

## 目的

4E 驗證 3.5、4A、4B、4C、4D 的正式核心能在隔離檔案工作區形成一致的 Package 管線，並確認跨階段證據不足時在任何正式候選寫入前停止。

## 整合情境

1. **完整成功路徑**：一次性 Package 流程完成客戶 Package 基準證據；Rule 1 以固定規則快照產生及封裝 `SourceDiff`／`TargetDiff`；Rule 2 驗證封裝、備份原始 `Solutions`、建立雙端 XML 工作副本並產生不可覆寫完成紀錄。
2. **跳點身分不一致**：4D 請求的來源／目標版本必須與 Rule 1 重用條件一致；不一致時不得建立備份、嘗試目錄或修改 `Solutions`。
3. **封裝竄改**：ZIP Checksum 不符時在備份及工作副本建立前阻擋。
4. **人工確認**：Rule 2 遇到無法唯一配對的 Scalar Property 時，無相依項目可繼續，但不得建立正式完成紀錄。

## 驗證邊界

- 客戶 Package 基準及兩端 OOTB Package 保持不可變。
- Package 非 XML 不比較；客戶來源非 XML 不複製，`Solutions` 原有非 XML 保持原樣。
- 規則版本、有效 Checksum、Rule 1 ZIP Checksum、備份位置及完成紀錄跨階段保留。
- 全部測試使用 `.test-output` 下的暫時工作目錄，不連接 DB、不啟動 Aras 工具、不操作 Core Tree 或 `K:`。

## 整合測試揭露的修正

`AdaptedPackageBuilder` 原先只驗證 Rule 1 ZIP 是否符合傳入的重用條件，沒有再比對 4D 請求本身宣告的跳點。4E 新增前置關卡，要求 `AdaptedPackageRequest.SourceVersion／TargetVersion` 與 `OotbHopDiffReuseRequirement` 完全一致；失敗發生於所有備份及寫入之前。

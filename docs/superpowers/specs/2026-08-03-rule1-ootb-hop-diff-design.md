# 4C Rule 1 OOTB 跳點差異包設計

## 目標與邊界

以兩個唯讀 OOTB `Solutions` 目錄及已發布的 Rule 1 規則版本為輸入，建立獨立工作產出，包含 `SourceDiff`、`TargetDiff`、處理摘要、完成標記及單一封裝檔 Checksum。不得修改原始 OOTB Package；只處理 XML，相對路徑不同的同名檔案不得配對，非 XML 不進入差異包。

## 架構

1. `Rule1DiffEngine` 處理一對 AML 文件。它以 `PackageCompareKeyIndex` 建立可靠一對一配對，依 `AmlSemanticComparer` 判斷完全相同或有差異，並在工作副本中套用 Rule 1：來源單側刪除、目標單側保留、相同雙刪、差異雙留。無法可靠配對的 Item 原樣保留並建立人工確認項目。
2. `OotbHopDiffBuilder` 使用 `PackageXmlPathMatcher` 掃描兩端 XML，逐檔讀取及處理，將結果寫到全新的嘗試目錄。單一 XML 解析或寫入失敗只阻擋該檔，其他檔案繼續；輸入與輸出不得重疊。
3. `OotbHopDiffPackager` 在沒有未處置人工確認與錯誤時寫入 `Completed` manifest，建立包含兩端、摘要與 manifest 的單一 ZIP，並對 ZIP 計算一個 SHA-256 Checksum。存在阻擋項目時只保存 `Incomplete` 摘要，不產生可重用封裝。
4. `OotbHopDiffArtifactVerifier` 以封裝 Checksum、來源／目標版本、Rule 1 規則版本及完成狀態驗證重用資格，不以目錄或檔名推定完成。

## 資料與狀態

- 每次建立使用新 `AttemptId` 與新輸出目錄，既有產出不可覆寫。
- 摘要記錄來源／目標根目錄、共同規則與版本例外的規則集 ID／版本／Checksum、有效規則 Checksum、開始與完成時間，以及保留、刪除、差異、錯誤、人工確認數量。
- 完成標記包含同一份摘要、人工確認是否已清空、封裝檔名與封裝 Checksum。
- 第一版不計算逐 XML Checksum，不修改 manifest，不刪除空 XML；空結果保留原 XML 宣告、Namespace 與空 `<AML>` Root。

## 錯誤與安全

- DTD、解析失敗、缺少 CompareKey、同側 Key 重複或非一對一配對均不可猜測。
- 工作輸出以新目錄建立；任一路徑重疊、既有輸出、符號連結／reparse point 或規則版本不符均阻擋開始。
- 4C 不執行 Rule 2、不修改 `Solutions`、不連接 DB、不操作 Core Tree 或 `K:`。

## 測試策略

- 以公開 seam 驗證 Item 四種 Rule 1 分類及深層 AML 保留。
- 驗證同名不同相對路徑不配對、非 XML 忽略、單檔錯誤隔離及輸入不變。
- 驗證存在人工確認時保持 `Incomplete`；成功時產出雙端、manifest、summary、ZIP 與可驗證 Checksum。
- 驗證封裝竄改、版本不符或非 Completed manifest 不可重用。

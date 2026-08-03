# 4C Rule 1 OOTB 跳點差異包

## 已完成能力

- `Rule1DiffEngine` 依 Package CompareKey 及 AML 語意比較處理四種 Item 結果：目標單側保留、來源單側刪除、相同雙刪、差異雙留。
- CompareKey 缺失、同側重複及不可靠 Scalar Property 配對均原樣保留並轉人工確認。
- `OotbHopDiffBuilder` 只讀來源與目標 OOTB `Solutions`，依 XML 相對路徑建立全新 `SourceDiff`／`TargetDiff`；非 XML 不進入差異包。
- 單一 XML 解析或安全寫入失敗時隔離該檔，其他檔繼續，整體保持 `Blocked`。
- `OotbHopDiffPackager` 在沒有錯誤或人工確認時產生完成標記、處理摘要及單一 ZIP，並計算一個 SHA-256 封裝 Checksum。
- `OotbHopDiffArtifactVerifier` 核對兩端內容邊界、處理摘要、完成狀態、來源／目標版本、共同規則與版本例外的固定版本、有效規則 Checksum 及封裝 Checksum。

## 安全邊界

原始 OOTB Package 不可修改；輸出不得與任一輸入重疊；每次執行使用不存在的新輸出路徑。空 AML 保留 XML，第一版不刪檔、不修改 Package manifest、不計算逐 XML Checksum。4C 不執行 Rule 2、不寫入客戶 `Solutions`，也不連接 DB、Core Tree 或 `K:`。

## 尚未包含

目前只有離線 .NET 核心與功能 Skill，尚無 UI／CLI、案件歷程 command/action 或實際客戶目錄執行器。後續整合受控執行時必須加入目錄鎖、執行快照與不可覆寫歷程，不得以直接呼叫核心取代案件關卡。

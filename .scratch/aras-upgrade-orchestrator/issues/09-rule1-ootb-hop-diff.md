# 09 Rule 1 OOTB 跳點差異包

Status: resolved

## 目標

以不可變的來源／目標 OOTB `Solutions` 及已發布 Rule 1 規則版本，建立可追溯、可攜式且可驗證重用的 `SourceDiff`／`TargetDiff` 跳點差異包。

## 驗收結果

- Rule 1 四種 Item 結果依規格處理，深層內容由 AML 語意比較判定。
- 不可靠 CompareKey 或 Scalar Property 配對不猜測，保留內容並建立人工確認。
- 只依 XML 相對路徑配對；非 XML 不比較也不進入差異包。
- 原始 OOTB Package 保持不變，每次使用新輸出目錄。
- 單檔錯誤隔離，其他檔案可繼續，但整體不得完成。
- 人工確認或錯誤存在時只保存 `Incomplete` 摘要，不產生可重用 ZIP。
- 成功產物包含兩端、處理摘要、完成標記、共同規則與版本例外快照，以及單一 ZIP Checksum。
- 重用驗證會拒絕竄改、版本不符或缺少任一端內容的封裝。
- `aras-prepare-ootb-hop-diff` 功能 Skill 只引用正式受測核心。

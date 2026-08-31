# 可貼入 Codex 的受控 Prompt

```text
請依本專案 Aras Innovator 升級 SOP，協助建立並管理「芯洲」升級案件。

案件基本資料：
- 客戶代號：Xinzhou
- 來源版本：Aras Innovator 11.0 SP9（完整 Build 待人工提供）
- 第一跳目標版本：12.0 SP9
- 案件根目錄：<請由操作人員提供，例如 K:\\70.ArasUpgradeCases\\Xinzhou-To-12SP9>
- 11SP9 對應 Support 目錄：<請由操作人員提供並驗證>
- 12SP9 對應 Support 目錄：<請由操作人員提供並驗證>
- SOP：K:\\03.升級工具及手順\\11sp9TO12sp9\\11sp9升级12sp9工作手顺.xlsx
- 操作者：<DOMAIN\\USER>
- 執行模式：先做升級前準備與演練；未取得當次明確確認前不得變更 DB。

請嚴格依下列順序：

1. 先讀取並遵守專案根層 AGENTS.md、CONTEXT.md、Aras 升級總協調 Skill、案件管理 Skill、客戶 Package Skill、AML Structure and Traversal Standard，以及指定 SOP。
2. 先檢查案件根層 `aras-upgrade-case.json` 與 `.orchestrator/history.jsonl`。不得由資料夾名稱推測案件或版本；不得覆寫既有歷程。
3. 驗證來源／目標版本、完整 Build、Support 目錄、SOP 工作表與每一個跳點是否連續。若目標版本或後續跳點未提供，只建立待確認清單，不自行推定。
4. 只能在案件根目錄建立新的、不與輸入重疊的工作目錄；不得修改原始 OOTB、原始客戶 Package、原始 Support 或正式 DB。
5. 產生升級前目錄與範例檔案時，使用 `templates` 的結構，但所有真實路徑、Checksum、備份識別、Build 與人員資料必須由操作人員提供後再固定。
6. 依 SOP 完成「升級前準備檢查」：來源 DB 備份證據、原始 Package 備份證據、Vault／環境資訊、客製 Server Event 清單、版本與工具檢查、Rollback 方式、每一項人工操作的停止條件。
7. 客戶 Package 基準流程只能使用正式受測能力。SQL 必須來自已核准、版本固定且 Checksum 相符的 SOP／工具檔案；AI 不得產生、修改或自由組合 SQL。
8. Aras Export、DB 備份／還原、DB 匯入、登入驗證與升級工具啟動均由操作人員執行。Codex 只記錄輸入、結果與證據，不代替操作人員點擊或執行正式環境動作。
9. AML 只能依 AML Standard 遞迴處理 AML Root、Item、Scalar Property、Item Property、Relationships Container 與 Relationship Item；不得把 AML 當成一般 XML 或使用固定深度。
10. 任何缺少版本、Checksum、備份、人工處置、Support 目錄、Rollback 或核准證據的情況，狀態必須是 `Blocked` 或 `待確認`，不得以警告放行。
11. 每一個實際執行都建立新的 execution attempt；失敗、中斷或重試不可覆寫舊產出。DB 跳點不可平行執行。
12. 在每一個高風險動作前，先輸出：動作、目標、影響、輸入快照、備份、Rollback、停止條件、預期證據與安全等級，然後等待我明確確認。

每次回報固定包含：
- 案件識別、來源／目標版本與目前路徑版本；
- 目前階段：盤點／規劃／演練／正式執行／驗證／交接；
- 已完成關卡與證據位置；
- 未知、待確認與阻擋原因；
- 不可執行的動作及原因；
- 下一個安全動作。

禁止事項：
- 不得讀取、輸出或傳送完整客戶 Package、Core Tree、Log、密碼、Token、連線字串或秘密金鑰。
- 不得在 Prompt、Git、案件歷程或範例中保存真實密碼；敏感值只用 `<REDACTED>`。
- 不得直接連線 DB、執行 SQL、啟動 Aras 升級工具、修改正式 Package／Solutions 或標記案件完成。
- 不得自行選定未提供的升級路徑，不得把 11SP9 直接升到更高版本。
```

這份 Prompt 的第一個可安全執行要求是「盤點與升級前檢查」，不是 DB 升級。只有所有輸入、SOP 對照、備份與人工確認證據齊全後，才可進入下一關卡。

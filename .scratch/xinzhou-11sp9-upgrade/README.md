# 芯洲 Aras Innovator 升級範例

本目錄是「芯洲、來源 11.0 SP9、第一跳目標 12.0 SP9」的安全範例，不是正式案件，也不包含客戶 Package、完整 Log、密碼、連線字串或真實 DB 資訊。

使用前請先由操作人員確認：

- 芯洲正式案件根目錄；
- 來源版本完整 Build；
- 目標版本及後續升級路徑；
- 11SP9 對應的正式 `Support` 目錄；
- DB 備份位置與人工操作人員；
- 是否只做演練，或已授權進入正式 DB 變更。

建議先從 [`codex-prompt.md`](codex-prompt.md) 開始。

相關範例：

- [`templates/directory-tree.md`](templates/directory-tree.md)：案件目錄配置。
- [`templates/aras-upgrade-case.json`](templates/aras-upgrade-case.json)：案件清單範例。
- [`templates/hop-11sp9-to-12sp9.json`](templates/hop-11sp9-to-12sp9.json)：第一跳模板。
- [`templates/preflight-evidence-checklist.md`](templates/preflight-evidence-checklist.md)：升級前證據清單。
- [`sop-mapping.md`](sop-mapping.md)：實際 SOP 工作表與本專案關卡對照。

本範例不會把 SOP 內的密碼或固定密碼當作可執行資料；所有敏感欄位均使用遮蔽值。

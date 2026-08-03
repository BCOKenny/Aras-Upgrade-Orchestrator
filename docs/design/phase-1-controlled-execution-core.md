# 第一階段：案件與受控執行核心

日期：2026-08-03  
依據：`.scratch/aras-upgrade-orchestrator/spec.md` 第 4、5、6、15、18 節

## 已建立的核心邊界

- `CaseManifest`：根層案件清單、版本化升級路徑、跳點 `Support` 目錄及產物位置。
- `TaskGraph`：明確區分跳點 Package 子任務與 DB 跳點執行；下一跳等待前一跳的人工登入驗證及 DB 備份證據。
- `AppendOnlyHistoryStore`：狀態轉換、確認、快照及結果以 JSON Lines 追加保存；更正是指向原事件的新事件。
- `ExecutionAttemptService`：每次運行建立獨立嘗試；中斷不自動續跑；失敗或中斷後只有 Idempotency、Rollback 或指定檢查點證據才能重試。
- `SafetyPolicy`：安全白名單、單次確認與阻擋三級判定；必要條件不足不能用確認繞過。
- `DirectoryLeaseManager`：相同或上下層重疊寫入目錄互斥，獨立目錄可平行。
- `IExternalActionExecutor`：危險與外部操作的唯一執行邊界；預設實作一律阻擋。
- `aras-innovator-upgrade`：主 Skill 僅負責階段、功能路由、安全關卡、證據、Rollback 與交接。
- `aras-manage-upgrade-case`：第一個功能 Skill，引用上述正式核心管理案件、路徑、任務、歷程、中斷與安全判定。

完整 Skill 名稱、輸入、輸出、安全責任及相依關係見 `docs/design/skill-map.md`。

## 案件目錄配置

```text
<客戶升級目錄>/
├─ aras-upgrade-case.json
└─ .orchestrator/
   ├─ history.jsonl
   └─ locks/
```

目前沒有建立或操作任何實際客戶案件目錄；測試只使用專案內的暫存測試資料並在測試後移除。

## 安全不變條件

1. 已存在的路徑版本不可修改或刪除；改變路徑只能追加新版。
2. 執行開始事件包含完整快照及 SHA-256 摘要，後續結果以新事件追加。
3. 單次確認綁定動作識別、版本、目標、輸入摘要、風險及前置條件。
4. 動作與快照不一致時，在建立執行嘗試前阻擋。
5. 外部執行器、白名單及目錄租約均由建構時明確注入，沒有隱含的 DB 或 Aras 連線。

## 尚未進入的後續階段

Package／AML、Rule 1／Rule 2、一次性 Package 流程鎖定、Core Tree 比較、桌面 UI 與最終交付組裝仍屬後續實作。這些正式程式能力必須與 Skill Map 對應的功能 Skill 同步建立；Package／AML 開始前須再次依 AML 共用標準進行規格衝突檢查。

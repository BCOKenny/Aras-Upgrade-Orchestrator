# Rule 1 預先比較與可用性確認設計

## 目的

將 Rule 1 OOTB 跳點差異處理拆為兩個獨立階段，使多個 Package 跳點可在實際 DB／Package Import 升級前先行比較；預先比較不依賴 Core Tree、客戶 DB、升級中間階段 DB、登入驗證、DB 備份或已執行的前一跳。

預先比較產生的結果不得直接作為 Rule 2 或 Package Import 的輸入。只有經過後續可用性確認的差異包，才能供 Rule 2 建立正式適配 Package。

本設計不修改任何 Core Tree 工作流、完成收據、交付內容或驗證規則。

## 範圍

### 包含

- 新增「Rule 1 預先比較」的案件層能力與狀態。
- 新增「Rule 1 可用性確認」的案件層能力與狀態。
- 允許預先比較使用案件外部或 `patch-support-input` 內已宣告的唯讀 OOTB `Solutions` 路徑。
- 允許不同跳點建立互不重疊的預先比較輸出。
- 保留現有 Rule 1 差異演算法、AML 規則、人工確認、錯誤隔離及 Rule 2 的正式輸入驗證。

### 不包含

- 連接或驗證客戶 DB。
- 啟動 Patch、Package Import、Aras Upgrade Tool、Aras Export 或 SQL。
- 修改客戶 Package 基準、Patch／Support 原始輸入或 OOTB `Solutions`。
- 修改 Core Tree 相關程式、文件或已完成產出。
- 自動判定跳點順序、版本、Patch 適用性或 OOTB 資料夾版本。

## 名詞與狀態

| 名稱 | 定義 | 是否可供 Rule 2 使用 |
|---|---|---:|
| Rule 1 預先比較 | 對操作人員明確指定的來源／目標 `Solutions` 執行唯讀差異處理。 | 否 |
| 預先比較結果 | `SourceDiff`、`TargetDiff`、處理摘要、錯誤與人工確認清單；狀態為 `Draft` 或 `PendingVerification`。 | 否 |
| Rule 1 可用性確認 | 針對既有預先比較結果，固定跳點版本、Rule 1 規則版本／Checksum 與輸入身分後的核定動作。 | 是，通過時 |
| 可重用差異包 | 含 `Completed` completion manifest、ZIP 與封裝 Checksum 的 Rule 1 產物。 | 是 |

## 流程

```text
操作人員提供每個完整跳點與唯讀 Solutions 路徑
  ↓
Rule 1 預先比較
  ├─ 建立新輸出目錄
  ├─ 產生 SourceDiff／TargetDiff、摘要、錯誤與人工確認
  └─ 寫入 Draft／PendingVerification 結果
  ↓
（可在任何時間，且不需 DB 升級）
  ↓
Rule 1 可用性確認
  ├─ 核對明確登錄的來源／目標版本與輸入身分
  ├─ 核對已發布 Rule 1 規則版本與 Checksum
  ├─ 確認無錯誤、無未處置人工確認
  └─ 建立 Completed manifest、ZIP 與 Checksum
  ↓
Rule 2 以已驗證差異包建立跳點適配 Package
```

## Rule 1 預先比較

### 最小前置條件

預先比較只要求：

1. 操作人員明確提供來源版本、目標版本、來源 `Solutions` 根目錄及目標 `Solutions` 根目錄。
2. 兩個輸入目錄均存在，且輸出目錄不存在並且不與任一輸入重疊。
3. 輸入與輸出均為可安全解析的路徑；不允許 `..`、磁碟根目錄或符號連結逃逸。
4. 以可解析且已發布的 Rule 1 規則快照執行 AML 差異處理；未發布、衝突或無法固定 Checksum 的規則必須拒絕執行，不得自動推測規則。
5. `packageComparisonPreparations` 的正式登錄為選用追溯資料；`--prepare-rule1` 必須可由 request 直接提供完整跳點與四個案件內相對路徑執行，不得以「尚未登錄」阻擋預先比較。

預先比較不得要求：

- `aras-upgrade-case.json` 中已建立正式 Package／DB `routes`。
- 連續升級路徑、前一跳完成、登入驗證、DB 備份或 DB Rollback 證據。
- 客戶 Package 基準。
- Core Tree 比較、完成收據或交付。
- Patch 已執行或目標版本 DB 已存在。

### 輸入登錄

每一跳由操作人員提供完整的 `sourceVersion` 與 `targetVersion`；系統不得根據前一跳、客戶來源版本、目錄名稱或 Patch 名稱推導。

`patch-support-input` 可作為 Rule 1 的來源或目標 `Solutions` 路徑來源，但必須以明確相對子路徑登錄，例如 `patch-support-input\\Support\\Solutions`。相同的唯讀 `Solutions` 輸入可被相鄰跳點引用，但不得被複製、修改或用資料夾名稱宣告版本正確。

### 結果規則

- 無錯誤且無人工確認：建立 `PendingVerification` 結果，不建立 `Completed` manifest 或 ZIP。
- 有錯誤或人工確認：建立 `Draft` 結果，保留已完成的獨立 XML 處理資訊，並阻擋可用性確認。
- 預先比較結果必須保存輸入路徑、宣告版本、Rule 1 規則快照（若有）、時間、摘要、錯誤及人工確認。
- 每次執行使用全新的 attempt 目錄，不覆寫任何既有結果。

## Rule 1 可用性確認

可用性確認不重跑 AML 比較，也不修改預先比較輸出。它只接受單一 `PendingVerification` 結果，並執行以下確認：

1. 操作人員登錄的來源／目標版本與預先比較結果相符。
2. 所引用的輸入路徑仍存在，且其不可變輸入摘要與預先比較時一致。
3. Rule 1 規則為已發布版本，且版本與有效 Checksum 已固定。
4. 預先比較結果為零錯誤、零人工確認。
5. ZIP、completion manifest 與 Checksum 的新輸出位置均不存在且不與輸入或 attempt 目錄重疊。

通過時才建立現行 `Completed` completion manifest、ZIP 與封裝 Checksum；產物仍必須通過 `OotbHopDiffArtifactVerifier`，才能成為 Rule 2 輸入。

## 安全與隔離

- Rule 1 預先比較及可用性確認均不連接 DB、不要求升級階段 DB，且不啟動升級工具。
- 預先比較與可用性確認均不得修改 OOTB `Solutions`、`patch-support-input`、客戶 Package 基準或 Core Tree。
- Rule 2 繼續要求：客戶 Package 基準、可重用差異包、跳點 `Support\\Solutions`、備份成功、Rule 2 規則快照與人工確認解除。
- 實際 Package Import 與 DB 跳點仍依既有順序執行，且每一跳仍等待登入驗證與 DB 備份證據。
- 未經可用性確認的結果不得被 Package Import、Rule 2 或最終交付引用。

## 案件與目錄模型

Package 預先比較可存放在既有案件的：

```text
package-upgrade/
  preparation-<id>/
    hops/
      <hop-slug>/
        ootb-source-solutions/
        ootb-target-solutions/
        patch-support-input/
        rule1-attempts/
        rule2-attempts/
```

案件層應新增獨立於 `UpgradeRoute` 的 `packageComparisonPreparations` 登錄。每筆至少包含：

- preparation ID、hop ID、來源／目標版本與輸入根目錄；
- 宣告來源（例如 `ootb-source-solutions` 或 `patch-support-input` 的相對子路徑）；
- 預先比較 attempt、狀態與不可變輸入摘要；
- 可用性確認收據及可重用差異包位置（若已完成）。

`packageComparisonPreparations` 不表示 DB 升級路徑，不可用來解除 DB 跳點或改寫 `UpgradeRoute`。

### 登錄草稿與正式登錄

- 案件專屬登錄草稿必須位於 `package-upgrade/<preparation-id>/manifests/`；專案 `docs/` 僅保存可重用 Prompt，不能保存客戶案件草稿。
- `rule-sets/` 只保存由規則管理流程發布的規則版本（`published/<RuleSetId>/<version>.json`）；不得保存跳點登錄草稿、CLI request 或 Package 輸入。
- 目錄骨架已決定的跳點版本、`preparationId` 與四個案件內相對路徑可由 Prompt 自動填入草稿。版本證據、規則版本／Checksum、attempt、whitelist、confirmation 與 artifact 路徑必須保持待人工填寫。
- `createdAt` 與 `createdBy` 是正式登錄事實，草稿不得填入。`--register-rule1-preparation` 必須以執行時間及具名 `actor` 寫入這兩個欄位，並追加 history。
- 正式登錄可在預先比較之前或之後補做，僅供案件追溯與後續可用性確認核對；它不是 `--prepare-rule1` 的前置條件。直接比較 request 不得藉此修改 `aras-upgrade-case.json`。

## 實作與驗收方向

### 正式能力

1. 擴充 `OotbHopDiffBuilder` 或其案件命令，以支援未發布規則時的 `PendingVerification` 輸出，但不得讓未固定規則產生可重用封裝。
2. 新增 `Rule1PreparationCommand`，負責輸入快照、目錄租約、新 attempt、不可覆寫歷程與 Draft／PendingVerification 產出。
3. 新增 `Rule1AvailabilityConfirmationCommand`，負責輸入摘要重驗、規則固定、零錯誤／人工確認檢查，以及交由 `OotbHopDiffPackager` 建立正式產物。
4. 新增受控 CLI／UI 入口；禁止直接用 Skill 或外部腳本呼叫 Rule 1 核心操作客戶案件目錄。

### 驗收案例

1. 沒有 DB、Core Tree、Package／DB route 或前一跳證據時，預先比較仍可建立 `PendingVerification` 結果。
2. 多個不同跳點、互不重疊輸出目錄時，可各自建立預先比較結果。
3. 預先比較輸入與輸出重疊、版本空白或路徑逃逸時拒絕執行。
4. 有 XML 錯誤或人工確認時，結果保持 `Draft`，不可核定或封裝。
5. 可用性確認在輸入摘要、跳點版本、規則版本或 Checksum 不符時拒絕封裝。
6. 可用性確認通過後產生的 ZIP 可被 `OotbHopDiffArtifactVerifier` 驗證並供 Rule 2 使用。
7. 未核定的預先比較結果傳入 Rule 2 時必須被拒絕，且 Rule 2 不得建立備份或修改 `Solutions`。
8. Core Tree 相關檔案、歷程與測試不受本功能影響。

## 文件影響

後續實作必須更新：

- `.scratch/aras-upgrade-orchestrator/spec.md` 的 Rule 1、規則、Package 人工確認與驗收條件。
- `docs/design/phase-4c-rule1-ootb-hop-diff.md`、`docs/design/phase-4e-package-integration-tests.md` 與 `docs/design/skill-map.md`。
- `docs/operations/case-management/codex-package-preparation-prompt.md`，加入輸入登錄與預先比較／可用性確認說明。

不得修改 Core Tree 專用文件或已完成的 Core Tree 產物。

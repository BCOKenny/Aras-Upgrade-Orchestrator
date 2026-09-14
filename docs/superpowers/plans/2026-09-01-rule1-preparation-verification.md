# Rule 1 預先比較與可用性確認實作計畫

> **給執行者：** 依序完成每一項工作；每項完成後執行該項的檢查，並在進行下一項前檢視結果。

**目標：** 將 Package 的 Rule 1 拆成「預先比較」與「可用性確認」兩個受控階段。預先比較可在升級、DB 階段與正式 Package 匯入之前獨立執行；只有可用性確認通過後，才產生可供 Rule 2 使用的正式 OOTB 差異 ZIP。

**架構：** 在既有案件模型中新增可追加、不可覆寫的 `packageComparisonPreparations` 計畫紀錄，與 `routes` 完全分離。Rule 1 預先比較將輸入快照、規則快照、結果及輸入樹狀雜湊寫入新的 attempt 目錄；可用性確認重新驗證這些不可變資料、解析已發布的 Rule 1 規則，然後重用既有封裝與驗證元件建立正式 artifact。

**技術：** .NET 8／C#、`ArasUpgradeOrchestrator.Core`、既有 append-only history／directory lease／safety policy、既有 `OotbHopDiffBuilder`、`OotbHopDiffPackager`、`OotbHopDiffArtifactVerifier`、自訂 console test runner。

**設計依據：** [Rule 1 預先比較與可用性確認設計](../specs/2026-09-01-rule1-preparation-verification-design.md)

## 全域約束

- 不修改、重跑或引用 Core Tree 比較流程、規格與交付物；Package 比較的前置流程必須獨立於 Core Tree。
- 不連線或操作 DB，不執行 Patch 匯入、Aras Import、Rule 2 或實際升級。
- AML 的讀取、遞迴與差異行為必須遵守 `docs/standards/AML_Structure_and_Traversal_Standard.md`；不得把 AML 當作一般 XML 階層處理。
- 所有 attempt、receipt、ZIP、manifest 與 history 都採新目錄／`FileMode.CreateNew` 寫入。絕不覆寫既有結果，也不讓輸出位於任何輸入目錄內。
- 允許不同跳點任意組合，例如 `11.0 SP8 → 11.0 SP15`、`12.0 SP18 → R38`；不得要求連續路徑、既有 `routes`、前一跳完成、DB 階段、客戶 baseline 或 Core Tree 成功。
- 預先比較不是升級核准。其結果只能是 `Draft` 或 `PendingVerification`，不得建立 Rule 2 可使用的 artifact。
- 依專案本機政策，實作時不得自行執行 `dotnet test`；計畫中的測試命令須在使用者明確授權測試後才可執行。

## 1. 擴充案件模型，保存獨立的 Package 比較計畫

**檔案：**

- 修改：`src/ArasUpgradeOrchestrator.Core/Cases/CaseManifest.cs`
- 修改：`src/ArasUpgradeOrchestrator.Core/Cases/CaseStore.cs`
- 修改：`tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**工作：**

1. 在 `CaseManifest` 的最後新增選擇性 `PackageComparisonPreparations` 欄位，JSON 名稱固定為 `packageComparisonPreparations`；缺少此欄位的既有 schema 1 案件必須仍可讀取。
2. 新增不可變的 `PackageComparisonPreparation` record，至少包含：
   - `PreparationId`：案件內唯一、可作為目錄名稱的 ID。
   - `SourceVersion`、`TargetVersion`：每跳完整輸入，且不得相同。
   - `OotbSourceSolutionsRelativePath`、`OotbTargetSolutionsRelativePath`：案件根目錄下的 OOTB Solutions 來源與目標位置。
   - `PatchSupportInputRelativePath`：該跳 Patch／Support 輸入根目錄；若 OOTB Solutions 位於此根目錄內，仍必須由前兩個欄位明確指出實際子目錄。
   - `PreparationAttemptRelativePath`：預先比較 attempt 的輸出根目錄。
   - `CreatedAt`、`CreatedBy`，以及可選的 `Notes`。
3. 不提高 `CurrentSchemaVersion`：此為可選、向後相容欄位，避免使已存在且只含 Core Tree 的案件無法開啟。
4. 在 `CaseStore` 新增 `AddPackageComparisonPreparationAsync`（或語意等同的方法）：只允許新增一筆完整、已驗證的計畫紀錄，禁止修改、刪除或取代既有紀錄。
5. 在 `CaseStore.Validate` 新增驗證：ID 唯一；版本字串非空且不同；所有相對路徑經既有 path-safety 邏輯解析後必須位於案件根目錄內；attempt 目錄不得與三個輸入根目錄重疊；不得把 preparation 路徑當成 `routes` 的替代規則。
6. 保持既有「route 與歷程不可覆寫」規則。新增 preparation 不得改變 `CurrentRouteVersion`、`Routes`、`CoreTreeComparison` 或已存在的 artifact locations。

**測試（先新增，再實作）：**

1. 讀取沒有 `packageComparisonPreparations` 的既有 Core Tree-only JSON，確認可成功反序列化且集合為空。
2. 對既有 Core Tree-only case 新增一個 preparation，確認 route 與 Core Tree 設定保持位元組等值語意不變。
3. 重複 PreparationId、相同來源／目標版本、空白欄位、跳出案件根目錄的路徑、輸出與輸入重疊，皆須被拒絕。
4. 兩個不連續跳點可同時登錄，且不需建立任何 `UpgradeRoute`。

## 2. 建立可重現的 Rule 1 預先比較輸入與狀態模型

**檔案：**

- 修改：`src/ArasUpgradeOrchestrator.Core/Packages/OotbHopDiffBuilder.cs`
- 新增：`src/ArasUpgradeOrchestrator.Core/Packages/Rule1PreparationModels.cs`
- 新增：`src/ArasUpgradeOrchestrator.Core/Packages/PackageInputTreeDigest.cs`
- 新增：`src/ArasUpgradeOrchestrator.Core/Packages/Rule1PreparationReceiptStore.cs`
- 修改：`tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**工作：**

1. 引入 `Rule1PreparationStatus`：
   - `Draft`：比較本身有 error、未解決的 manual review，或規則僅為候選快照。
   - `PendingVerification`：比較結果無 error、無未解決 manual review，但尚未完成正式可用性確認。
   - `Completed` 僅保留給既有正式 artifact；不可作為預先比較 receipt 狀態。
2. 將 Rule 1 執行規則抽象為明確的不可變快照：候選快照可以提供實際 Rule 1 步驟供比較使用，但沒有已發布規則版本與有效 checksum；已發布快照必須同時具備版本、內容與有效 checksum。不得在執行時自動補寫或推測規則。
3. 調整 `OotbHopDiffBuilder` 的 request／result，使它可接受上述規則快照並產生「可供確認」結果；保留既有 XML 比較與 AML traversal 邏輯，僅調整狀態與中繼資料，不改變 Rule 1 差異演算法。
4. 實作 `PackageInputTreeDigest.ComputeAsync(root)`：
   - 只接受已存在的普通目錄，拒絕 reparse point／符號連結與無法讀取的檔案。
   - 依序走訪所有一般檔案，按 ordinal 相對路徑排序。
   - 對每個檔案計算 SHA-256，並以「相對路徑、檔案長度、檔案雜湊」的長度前綴 canonical stream 計算整棵樹 SHA-256。
   - receipt 必須保存根目錄相對路徑、檔案數、總位元組數與樹狀雜湊，讓確認階段可精確檢測任何輸入變動。
5. 實作 `Rule1PreparationReceiptStore`，以新目錄寫入 `preparation-manifest.json` 和既有格式相容的 `processing-summary.json`。manifest 至少保存：案件／preparation／attempt ID、版本、三個輸入描述與 digest、規則快照狀態、結果狀態、errors、manual reviews、建立者與建立時間。
6. receipt 只能以新 attempt 目錄建立；目錄已存在、輸出位於輸入根目錄內、或輸入彼此重疊時一律失敗。

**測試（先新增，再實作）：**

1. 使用相同三個輸入樹兩次計算 digest，結果必須穩定；變更任一檔案或新增檔案後，digest 必須改變。
2. 空目錄、reparse point、檔案讀取失敗與不安全路徑必須明確失敗，不能產生部分 receipt。
3. 候選規則快照且比較乾淨時結果仍是 `PendingVerification`；有 error 或 manual review 時為 `Draft`。
4. 預先比較不會建立 ZIP、`completion-manifest.json` 或 `Completed` artifact。
5. 再次使用同一 attempt 輸出目錄必須被拒絕，原 receipt 不得被改寫。

## 3. 實作 Rule 1 預先比較受控命令

**檔案：**

- 新增：`src/ArasUpgradeOrchestrator.Core/Packages/Rule1PreparationCommand.cs`
- 修改：`src/ArasUpgradeOrchestrator.Core/Execution/History.cs`
- 修改：`tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**工作：**

1. 新增 `Rule1PreparationCommandRequest`，輸入只包含 `CaseRoot`、`PreparationId`、`AttemptId`、候選或已發布 Rule 1 快照、操作者及必要的理由／備註；不得接受 DB、連線字串、帳密、Import 或 upgrade action。
2. 命令先由 `CaseStore` 載入指定 preparation，僅依其登錄路徑解析 OOTB source、OOTB target 與 patch/support input；命令不得根據目錄名稱猜測版本或跳點。
3. 使用既有 `DirectoryLeaseManager`、`ControlledAction` 與 `SafetyPolicy` 的同等保護，建立新 attempt 時先寫 append-only `package.rule1-preparation.started` history event，再執行比較與 receipt 寫入。
4. 預先比較只檢查：完整 source／target 宣告、三個目錄存在且安全、輸出新且不重疊、三個輸入 digest 可取得、規則快照可解析、AML／比較錯誤與人工覆核結果。明確不檢查：升級 route、前跳、DB 階段、customer baseline、Core Tree、Patch import 或目標 DB。
5. 成功寫入 receipt 後追加：
   - `package.rule1-preparation.pending-verification`，用於乾淨比較；或
   - `package.rule1-preparation.draft`，用於 error／manual review／候選規則。
   事件 payload 僅放 attempt 相對路徑、版本、狀態、輸入樹狀雜湊與摘要計數，不寫入 Package 內容或敏感資料。
6. 任一檢查失敗時追加受控 blocked／failed history event；不得留下可被誤認為完成的 manifest。

**測試（先新增，再實作）：**

1. 沒有 `routes`、沒有 customer baseline、沒有 Core Tree 完成紀錄的 case 仍可成功執行預先比較。
2. 使用兩個非連續 preparation 時，各自會建立獨立 attempt 與 history，互不需要前一跳完成。
3. 輸入遺失、版本未完整登錄、輸出已存在、輸出與輸入重疊、規則快照無法解析時，命令應停止且不寫完成 receipt。
4. 乾淨比較結果為 `PendingVerification`，history 不得含 `completed` 或任何 Rule 2 可引用的 artifact。

## 4. 實作 Rule 1 可用性確認與正式 artifact 建立

**檔案：**

- 新增：`src/ArasUpgradeOrchestrator.Core/Packages/Rule1AvailabilityConfirmationCommand.cs`
- 修改：`src/ArasUpgradeOrchestrator.Core/Packages/OotbHopDiffArtifact.cs`
- 修改：`src/ArasUpgradeOrchestrator.Core/Packages/OotbHopDiffPackager.cs`
- 修改：`src/ArasUpgradeOrchestrator.Core/Packages/OotbHopDiffArtifactVerifier.cs`
- 修改：`src/ArasUpgradeOrchestrator.Core/Execution/History.cs`
- 修改：`tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**工作：**

1. 新增確認 request，至少包含 `CaseRoot`、`PreparationId`、`AttemptRelativePath`、新的 `ArtifactRelativePath`、操作者及已發布 Rule 1 規則解析來源。artifact 一律輸出到 preparation 定義的 attempt 根目錄外，例如 `rule1-artifacts/<artifact-id>.zip`；不得放入 source、target、patch/support 或 attempt 目錄。
2. 只允許讀取狀態為 `PendingVerification` 的 receipt。`Draft` 必須先以新的 attempt 重跑，不能直接確認或修改成完成。
3. 重新計算 receipt 中三個輸入根目錄的 digest，必須逐一與 receipt 相同；任何不一致都停止並記錄 `package.rule1-availability-confirmation.blocked`，不得封裝舊結果。
4. 透過既有 `RuleSetResolver` 解析已發布的 Rule 1 規則；要求版本、每個 rule set 的內容 checksum 及 effective checksum 都固定存在，且與 receipt 的步驟語意一致。候選規則、遺失 checksum 或規則漂移一律停止。
5. 再次確認 receipt 沒有 errors 或未解決 manual reviews，然後重用 `OotbHopDiffPackager` 建立 ZIP 與 `completion-manifest.json`，並立即用 `OotbHopDiffArtifactVerifier` 驗證 ZIP checksum、版本、已發布規則與 effective checksum。
6. 僅在驗證成功後追加 `package.rule1-availability-confirmation.completed` history event，payload 保存 preparation／attempt／artifact 相對路徑、artifact checksum、規則版本及 effective checksum。此事件與 verified artifact 才是 Rule 2 的唯一可用輸入。
7. 不改變 `AdaptedPackageBuilder` 的既有前提：它仍必須接受已驗證的 `Completed` Rule 1 artifact；它不得接受 receipt、Draft 或 PendingVerification。

**測試（先新增，再實作）：**

1. `PendingVerification` receipt + 未變動輸入 + 已發布固定規則，可建立並驗證 `Completed` ZIP。
2. 任一輸入檔變動、刪除或新增後，確認必須拒絕且不建立 artifact。
3. receipt 為 Draft、有 error、有 manual review、規則未發布、checksum 缺失、effective checksum 漂移、規則步驟漂移、artifact 目錄已存在，皆必須拒絕。
4. 成功 artifact 可被既有 Rule 2 verifier／`AdaptedPackageBuilder` 接受；預先 receipt 則仍被拒絕。

## 5. 提供受控 CLI 入口與輸入檔格式

**檔案：**

- 新增：`tools/ArasUpgradeOrchestrator.Package.Cli/ArasUpgradeOrchestrator.Package.Cli.csproj`
- 新增：`tools/ArasUpgradeOrchestrator.Package.Cli/Program.cs`
- 修改：`ArasUpgradeOrchestrator.sln`
- 修改：`tests/ArasUpgradeOrchestrator.Core.Tests/Program.cs`

**工作：**

1. 以既有 Core Tree CLI 的輸入驗證與 exit-code 慣例建立 Package CLI，不共用 Core Tree command 或 request schema。
2. 提供兩個明確且互斥的指令：
   - `--prepare-rule1 <request.json>`：執行預先比較，只產生 receipt。
   - `--confirm-rule1 <request.json>`：執行可用性確認，只產生正式 artifact。
3. request JSON 要求顯式填寫 `caseRoot`、`preparationId`、`attemptId`、操作者與規則快照／已發布規則來源；版本與資料夾一律從案件登錄讀取，CLI 不接受「只有目標版」或根據資料夾名稱推斷跳點。
4. CLI 的 stdout 僅輸出結構化成功摘要（狀態、attempt、artifact 如有、checksum 如有）；錯誤輸出不得含 Package XML 內容、絕對敏感路徑、帳密、token 或 connection string。
5. 對未知參數、兩個動作同時指定、缺少必要欄位、request 路徑不存在與非法 JSON 提供明確非零 exit code。

**測試（先新增，再實作）：**

1. 以最小 JSON request 執行 `--prepare-rule1`，確認只輸出 PendingVerification receipt。
2. 使用已發布規則 request 執行 `--confirm-rule1`，確認輸出 verified artifact 摘要。
3. 同時指定兩個動作、缺欄位、非法 JSON 與嘗試確認 Draft receipt 均應有非零 exit code。

## 6. 同步規格、操作文件與範本

**檔案：**

- 修改：`.scratch/aras-upgrade-orchestrator/spec.md`
- 修改：`docs/roadmap/phase-4c-ootb-hop-diff.md`
- 修改：`docs/roadmap/phase-4e-package-upgrade.md`
- 修改：`docs/design/skill-map.md`
- 修改：`docs/operations/case-management/codex-package-preparation-prompt.md`
- 修改：`docs/superpowers/specs/2026-09-01-rule1-preparation-verification-design.md`（僅在實作與核准設計間發現必要差異時）

**工作：**

1. 將 Rule 1 文件改為明確的雙階段：預先比較可先行、無 DB／路由／Core Tree gate；可用性確認才固定已發布規則與完成 artifact。
2. 說明 `patch-support-input` 是跳點專屬輸入暫存位置，`ootb-source-solutions`／`ootb-target-solutions` 必須明確登錄，不能僅由 `patch-support-input` 或目錄名推斷。
3. 保留 shared `customer-package-baseline/<customer-id>/input` 的用途，但明定它不是 Rule 1 預先比較 gate，且不應被 Rule 1 預先比較讀取。
4. 在範本加入「每跳完整輸入」範例，包含 `11.0 SP8 → 11.0 SP15`、`12.0 SP18 → R38`；不得假設任何固定跳點組合。
5. 清楚標示 Rule 2 僅可使用可用性確認後的 verified ZIP；Core Tree 規格、Core Tree CLI 與 Core Tree 交付文件不在本次變更範圍。

## 7. 實作檢視與驗收

**檔案：** 僅檢視本計畫列出的修改檔與新增檔。

**工作：**

1. 以 `rg` 檢查 `PendingVerification`、`Completed`、`packageComparisonPreparations`、history event 名稱與 CLI 參數在程式、測試與文件中的一致性。
2. 逐項檢視所有 `FileMode.CreateNew`、輸入／輸出不重疊、case-root path-safety、receipt digest 重驗、已發布規則 checksum 與 Rule 2 artifact gate。
3. 確認 diff 沒有更動任何 Core Tree production code、Core Tree CLI 行為或 Core Tree delivery artifact 格式。
4. 在使用者明確授權後，執行：

   ```powershell
   dotnet test ArasUpgradeOrchestrator.sln
   ```

5. 若測試失敗，先保留失敗輸出與最小重現案例；依專案既有測試 runner 的慣例修正，再重新執行授權的測試。不得以刪除測試或放寬 gate 使測試通過。
6. 完成時回報：新增／修改檔案、預先比較與確認各自的行為邊界、測試是否已獲授權並實際執行，以及任何仍需人工提供的已發布 Rule 1 規則快照。

## 交付驗收標準

- 使用者能在沒有 DB、upgrade route、customer baseline 或 Core Tree gate 的案件中，為任意完整跳點建立 Rule 1 預先比較 receipt。
- `PendingVerification` 不會被誤用為 Rule 2 輸入，也不會產生正式 ZIP。
- 可用性確認能偵測輸入或規則的任何漂移；只有未變動輸入與固定已發布規則才會產出 verified artifact。
- 既有 Core Tree-only case 與既有 Completed Rule 1／Rule 2 行為仍可讀取並受原有保護。

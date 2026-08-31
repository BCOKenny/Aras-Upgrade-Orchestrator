# Codex Prompt：建立客戶升級案件目錄與範本

本 Prompt 用於未來依不同客戶、來源版本與最終版本建立案件骨架。

建立完成後，由操作人員手動將客戶 Core Tree、各版本 OOTB Core Tree、版本證據與其他輸入放入指定目錄。Codex 不自動複製、搬移、解析或上傳客戶資料。

## 使用方式

將下方 Prompt 貼入 Codex，替換「案件參數」中的內容。

## 只驗證客戶與版本參數

若目前只是確認 Prompt 是否能正確套用不同客戶或 Aras Innovator 版本，請將執行模式明確設為：

```text
執行模式：VALIDATE_ONLY
```

此模式只驗證參數與推導出的預期路徑，不建立任何實際目錄或檔案，也不要求案件目錄、正式案件建立 UI／CLI、完整 SOP、Package／DB 路徑或既有案件歷程已存在。驗證結果應包含：

- 客戶代號、案件目錄名稱、來源／目標版本是否有值且不含 `<...>` 未替換佔位符；
- 來源／目標版本短名稱是否為小寫且可安全用於目錄名稱；
- 案件目標路徑是否可由案件根目錄與案件目錄名稱正確推導，且不是案件根目錄本身或其他廣泛路徑；
- `customer-<SOURCE_SLUG>`、`ootb-<SOURCE_SLUG>`、`ootb-<TARGET_SLUG>` 與比較名稱的預期值；
- 若切換為建立模式，仍需補充的正式案件入口、Build／版本證據、SOP、Package／DB 路徑與操作者資訊。

`VALIDATE_ONLY` 完成後立即結束，回報 `VALIDATION_ONLY: PASS` 或具體錯誤；不要進入後面的「建立案件」規則，也不要因實際路徑不存在而判定失敗。這個模式不會執行 Core Tree preflight、Package Import、DB、SQL 或任何升級作業。

### `VALIDATE_ONLY` 強制分支

當執行模式等於 `VALIDATE_ONLY` 時，以下規則具有最高優先順序：

1. 只驗證本 Prompt 列出的參數格式、版本短名稱、案件目標路徑推導、外部根目錄是否為明確固定路徑與三個 Core Tree 識別。
2. 不得載入或驗證正式 `CaseManifest`，不得要求至少一個升級跳點。
3. 不得要求或判定正式 SOP、Package／DB 路徑、官方 Patch、Support、Build、DB 備份或既有案件歷程。
4. 不得因案件目錄不存在、案件清單不存在、歷程不存在或鎖不存在而判定 `Blocked`；這些只能列為資訊。
5. 所有參數與路徑格式通過時，結果必須是 `VALIDATION_ONLY: PASS`。只有佔位符、空值、來源／目標相同、非法短名稱或路徑逃逸等驗證錯誤，才可回報 `VALIDATION_ONLY: INVALID`。
6. 輸出驗證結果後立即停止，不得繼續閱讀本文件的 `SCAFFOLD_ONLY` 建立規則。

## 專案外案件根目錄

案件資料可以放在專案目錄外，例如：

```text
K:\70.ArasUpgradeCases
```

但這不是放寬任意外部寫入。`DIRECTORY_SCAFFOLD_ONLY` 使用使用者在本次要求中明確指定的固定案件根目錄，並通過下列檢查：

1. 路徑是使用者明確指定的固定目錄，不是任意輸入拼接出的廣泛根目錄。
2. 案件目標是該固定根目錄下的子目錄，例如 `K:\70.ArasUpgradeCases\芯洲`。
3. 執行環境已對該固定根目錄授予本次必要的寫入權限；尚未授權時，Codex 必須請求該根目錄的提升權限。
4. 建立前仍須檢查既有案件清單、歷程、鎖及非本案件資料，不得覆寫或刪除。
5. 專案原始碼、文件與設定仍只能修改專案目錄；外部根目錄只保存案件資料與產出。

`DIRECTORY_SCAFFOLD_ONLY` 本身是受控的正式目錄建立動作，不需要另一個 UI／CLI／command/action。若使用者沒有明確指定固定根目錄，或執行環境拒絕該根目錄的寫入權限，才必須停止並回報「外部案件根目錄未授權」；不可改寫到專案目錄，也不可用手工方式繞過。`VALIDATE_ONLY` 仍可只驗證路徑格式，不進行寫入。

## 芯洲可直接套用的案件參數

若本次案件就是目前指定的芯洲案件，可先使用以下參數，不要保留 `<CASE_ROOT>` 或 `<CASE_DIRECTORY_NAME>`：

```text
客戶代號：芯洲
案件目錄名稱：芯洲
案件根目錄：K:\70.ArasUpgradeCases
來源版本：11.0 SP9
來源版本短名稱：11sp9
最終版本：R38
最終版本短名稱：r38
操作者：BCO\kenny（若實際操作者不同，請替換）
執行模式：只建立目錄、案件清單範本與證據範本，不執行升級
```

完成後預期案件根目錄為：

```text
K:\70.ArasUpgradeCases\芯洲
```

注意：`11.0 SP9` 與 `R38` 是案件識別用版本名稱；正式 Core Tree preflight 前仍必須補齊實際 Build、版本證據、三份輸入目錄及 `Innovator\Client`／`Innovator\Server` 結構。`BCO\kenny` 只是依目前專案歷程提供的範例操作者，若不正確必須替換。

若尚未提供完整 Build，案件骨架可以先標記為 `待確認`，但不可進入正式比較或 Package／DB 升級。

```text
請使用本專案的 Aras Innovator 升級規格，建立一個新的客戶升級案件目錄骨架與範本。

案件參數：
- 客戶代號：<CUSTOMER_CODE>
- 案件目錄名稱：<CASE_DIRECTORY_NAME>
- 案件根目錄：<CASE_ROOT>
- 本次外部案件根目錄授權：使用者明確授權／未授權（建立模式必填）
- 來源版本：<SOURCE_VERSION，例如 11.0 SP9>
- 來源版本短名稱：<SOURCE_SLUG，例如 11sp9>
- 最終版本：<TARGET_VERSION，例如 R38>
- 最終版本短名稱：<TARGET_SLUG，例如 r38>
- 操作者：<DOMAIN\\USER>
- 執行模式：VALIDATE_ONLY、DIRECTORY_SCAFFOLD_ONLY 或 SCAFFOLD_ONLY

執行模式規則：
- `VALIDATE_ONLY`：只驗證參數與預期路徑，驗證後立即結束，不建立檔案。
- `DIRECTORY_SCAFFOLD_ONLY`：在使用者明確授權固定根目錄、執行環境寫入權限與安全檢查通過後，只建立目錄及非正式範本，不建立正式案件清單或歷程；它本身就是正式目錄建立動作，不要求另一個 command/action。
- `SCAFFOLD_ONLY`：建立目錄、案件清單範本與證據範本，不執行升級；此模式才適用後續「建立案件」規則。

三種模式不得互換：

| 執行模式 | 允許產出 | 禁止事項 |
|---|---|---|
| `VALIDATE_ONLY` | 參數、路徑與預期識別的驗證結果 | 任何寫入、目錄建立及正式案件檢查 |
| `DIRECTORY_SCAFFOLD_ONLY` | 案件目錄、工作目錄與非正式範本 | `aras-upgrade-case.json`、`.orchestrator\\history.jsonl`、正式案件清單、正式歷程及升級執行 |
| `SCAFFOLD_ONLY` | 依正式案件能力建立案件清單與相關範本 | 未具備正式案件建立能力時不得使用 |

`DIRECTORY_SCAFFOLD_ONLY` 的非正式範本不得被描述為正式案件清單，也不得回報案件已正式建立。若正式案件建立能力尚未存在，必須使用 `DIRECTORY_SCAFFOLD_ONLY`。

若本次只是驗證客戶或版本替換，必須使用 `VALIDATE_ONLY`，不可使用建立模式。

若本次要實際建立目錄與範本，但尚未有正式案件建立 UI／CLI 或完整升級路徑，請使用：

```text
執行模式：DIRECTORY_SCAFFOLD_ONLY
```

此模式只建立目錄與非正式範本，不建立正式 `aras-upgrade-case.json`、不寫入 `.orchestrator\history.jsonl`，也不要求連續升級跳點、正式 SOP、Patch 或 Support 證據。只有 `SCAFFOLD_ONLY` 才代表已有正式案件建立能力並進入正式案件骨架規則。

### `DIRECTORY_SCAFFOLD_ONLY` 的 PowerShell 執行規則

若以 PowerShell 建立目錄，請在寫入前先完成使用者明確授權的固定根目錄、執行環境寫入權限、既有資料與覆寫風險檢查。通過後只執行一份固定的建立腳本；不得以臨時改寫的一行指令反覆重試。

若一般執行環境因專案外路徑而拒絕寫入，Codex 必須對該使用者明確指定的固定根目錄請求一次提升權限，並在核准後執行同一份固定腳本。不得回報「缺少外部根目錄白名單」或「缺少 DIRECTORY_SCAFFOLD_ONLY command/action」，除非使用者沒有明確指定根目錄，或提升權限請求被拒絕。

- 路徑組合一律使用 `Join-Path -Path <父路徑> -ChildPath <子路徑>`。
- 檢查既有路徑可使用 `Test-Path -LiteralPath`。
- 建立目錄一律使用 `[System.IO.Directory]::CreateDirectory(<完整路徑>)`；不得使用 `New-Item -LiteralPath`，因為 `New-Item` 不支援此參數。
- 每一個預定目錄都以相對於案件根目錄的固定清單建立；不得以萬用字元、`..` 或未驗證字串組合路徑。
- 腳本檔必須以 UTF-8 with BOM 寫入；可執行腳本來源必須只含 ASCII 字元。中文案件名稱、中文輸出與中文範本內容不得直接放入腳本來源，需以 Unicode code point 或 ASCII-only 內容在執行期產生。
- 客戶或案件名稱含非 ASCII 字元時，不得直接寫入腳本來源、命令列或 JavaScript 字串。必須以 Unicode code point 數字陣列重建，例如案件名稱「芯洲」使用 `33455,27954`，並以 `[char]::ConvertFromUtf32()` 組合。
- 先在記憶體完整推導案件名稱、目標路徑、全部預定目錄與範本檔，逐一確認不存在且都位於固定案件根目錄內；同時確認正式案件清單、正式歷程、鎖與其他既有資料不會被覆寫。所有預檢通過前不得建立任何目錄或檔案。
- 預檢必須涵蓋所有預定目錄、所有預定範本檔、正式禁止檔案及目標案件目錄狀態；不得先建立目錄再補做檢查。
- 建立範本前先確認目標檔案不存在；若腳本失敗，停止並輸出已建立／未建立清單。不得自行變更指令語法後直接重跑，也不得自動刪除含錯誤字元的部分產物；清理前必須列出唯一完整路徑並取得使用者明確核准。

固定建立模式如下：

```powershell
$target = Join-Path -Path $caseRoot -ChildPath $relativeDirectory
[System.IO.Directory]::CreateDirectory($target) | Out-Null
```

非 ASCII 案件名稱的固定組合模式如下；此段可執行內容只含 ASCII：

```powershell
$caseNameCodePoints = [int[]](33455,27954)
$caseName = [string]::Concat(($caseNameCodePoints | ForEach-Object { [char]::ConvertFromUtf32($_) }))
```

### Windows 路徑與本地編排傳遞規則

本地編排層可能在送出命令前再次解析 Markdown 與 JavaScript。因此，任何會送往執行器的 JavaScript 或 PowerShell 文字都不得包含反引號，也不得使用 JavaScript template literal 或 `String.raw`。Markdown 說明、行內程式碼標記與程式碼圍欄不得直接拼入執行內容。

Windows 路徑只能使用已跳脫反斜線的普通 JavaScript 字串，例如：

```javascript
const caseRoot = "K:\\70.ArasUpgradeCases";
```

腳本內容與 Markdown 必須分離：不得把 Markdown 行內程式碼標記、Markdown 程式碼圍欄或對話原文直接拼進執行命令。先以純文字產生並完成解析的暫存 `.ps1` 檔，再使用 `powershell -File` 執行；路徑以參數或變數傳入。腳本必須是本次固定版本；若編排層或腳本回報錯誤，停止且回報，不得臨時改寫語法、轉義或路徑後自動重試。

例如，只驗證另一個客戶由 11.0 SP12 到 R38 的參數替換，可使用：

```text
客戶代號：<實際客戶代號>
案件目錄名稱：<實際案件目錄名稱>
案件根目錄：K:\70.ArasUpgradeCases
來源版本：11.0 SP12
來源版本短名稱：11sp12
最終版本：R38
最終版本短名稱：r38
操作者：BCO\kenny
執行模式：VALIDATE_ONLY
```

這個範例只會推導出 `K:\70.ArasUpgradeCases\<實際案件目錄名稱>` 與三個 Core Tree 預期輸入識別，不會因目錄尚未建立而停止，也不會建立該目錄。

如果使用上方的芯洲範例，案件參數應實際填寫為：

```text
客戶代號：芯洲
案件目錄名稱：芯洲
案件根目錄：K:\70.ArasUpgradeCases
來源版本：11.0 SP9
來源版本短名稱：11sp9
最終版本：R38
最終版本短名稱：r38
操作者：BCO\kenny
```

若執行模式是 `SCAFFOLD_ONLY`，請先閱讀：
1. AGENTS.md
2. CONTEXT.md
3. docs/agents/domain.md
4. docs/standards/AML_Structure_and_Traversal_Standard.md
5. docs/design/skill-map.md
6. .scratch/aras-upgrade-orchestrator/spec.md
7. docs/adr/0004-core-tree-and-package-upgrade-separation.md
8. 與本案件來源／目標版本相符的正式 SOP；若找不到或無法確認，標記為「待確認」，不可自行推定

若執行模式是 `VALIDATE_ONLY`，只需依本 Prompt 驗證參數格式與路徑推導，不需讀取或驗證正式升級 SOP。

若執行模式是 `VALIDATE_ONLY`，必須依「`VALIDATE_ONLY` 強制分支」完成參數驗證與預期路徑輸出後立即結束，不執行以下建立案件規則。只有 `SCAFFOLD_ONLY` 才繼續執行以下內容。

若執行模式是 `DIRECTORY_SCAFFOLD_ONLY`，只執行「Core Tree 目錄」、「Package／DB 升級目錄」、「案件管理目錄」及「範本檔案」中不含正式案件清單與正式歷程的部分；完成後立即結束。不得載入 `CaseManifest`，不得要求升級跳點或正式 SOP，不得建立 `aras-upgrade-case.json` 或 `.orchestrator\history.jsonl`。

請依下列規則建立案件：

一、案件根目錄

案件根目錄為：
<CASE_ROOT>\\<CASE_DIRECTORY_NAME>

建立前先檢查：
- 若 `<CASE_ROOT>` 位於專案目錄外，先確認使用者已在本次要求明確授權這個固定完整根目錄，並請求或確認執行環境的寫入權限；
- 目標路徑是否已存在案件清單；
- 是否存在其他案件或非本案件資料；
- 路徑是否在使用者指定的案件根目錄範圍內；
- 是否有目錄鎖或既有執行歷程。

若路徑已有 `aras-upgrade-case.json`、`.orchestrator\\history.jsonl` 或不可判定的既有資料，停止並回報，不覆寫、不刪除、不合併。

二、Core Tree 目錄

Core Tree 只做「客戶目前版本」與「最終版本」的版本差異比較，不依中間 Package／DB 跳點建立目錄。

請建立：

<CASE_ROOT>\\<CASE_DIRECTORY_NAME>\\core-tree\\inputs\\customer-<SOURCE_SLUG>\\tree\\
<CASE_ROOT>\\<CASE_DIRECTORY_NAME>\\core-tree\\inputs\\customer-<SOURCE_SLUG>\\evidence\\
<CASE_ROOT>\\<CASE_DIRECTORY_NAME>\\core-tree\\inputs\\ootb-<SOURCE_SLUG>\\tree\\
<CASE_ROOT>\\<CASE_DIRECTORY_NAME>\\core-tree\\inputs\\ootb-<SOURCE_SLUG>\\evidence\\
<CASE_ROOT>\\<CASE_DIRECTORY_NAME>\\core-tree\\inputs\\ootb-<TARGET_SLUG>\\tree\\
<CASE_ROOT>\\<CASE_DIRECTORY_NAME>\\core-tree\\inputs\\ootb-<TARGET_SLUG>\\evidence\\

並建立空的工作目錄：

<CASE_ROOT>\\<CASE_DIRECTORY_NAME>\\core-tree\\attempts\\
<CASE_ROOT>\\<CASE_DIRECTORY_NAME>\\core-tree\\completions\\
<CASE_ROOT>\\<CASE_DIRECTORY_NAME>\\core-tree\\deliveries\\
<CASE_ROOT>\\<CASE_DIRECTORY_NAME>\\core-tree\\requests\\
<CASE_ROOT>\\<CASE_DIRECTORY_NAME>\\core-tree\\review-approvals\\

三、Package／DB 升級目錄

Package／DB 升級與 Core Tree 分開保存。請建立：

<CASE_ROOT>\\<CASE_DIRECTORY_NAME>\\package-upgrade\\customer-baseline\\
<CASE_ROOT>\\<CASE_DIRECTORY_NAME>\\package-upgrade\\hops\\
<CASE_ROOT>\\<CASE_DIRECTORY_NAME>\\package-upgrade\\pending-route\\
<CASE_ROOT>\\<CASE_DIRECTORY_NAME>\\db-upgrade\\hops\\
<CASE_ROOT>\\<CASE_DIRECTORY_NAME>\\handoff\\

不要自行產生或填寫 Package／DB 跳點。只有在操作人員提供並驗證 Aras 官方 SOP、Patch、Support 與連續版本路徑後，才建立各跳點子目錄。

四、案件管理目錄

請建立：

<CASE_ROOT>\\<CASE_DIRECTORY_NAME>\\.orchestrator\\
<CASE_ROOT>\\<CASE_DIRECTORY_NAME>\\.orchestrator\\locks\\
<CASE_ROOT>\\<CASE_DIRECTORY_NAME>\\inputs\\source-<SOURCE_SLUG>\\version-evidence\\
<CASE_ROOT>\\<CASE_DIRECTORY_NAME>\\inputs\\source-<SOURCE_SLUG>\\db-backup-evidence\\

五、案件清單

使用本專案正式案件模型建立 `aras-upgrade-case.json` 範本，至少包含：

- 新產生的案件 GUID；
- 客戶代號；
- 完整來源版本與最終版本；
- 案件根目錄；
- `coreTreeComparison`；
- 目前 Package／DB 路徑版本；
- Core Tree 三份輸入的案件內相對路徑；
- 建立時間；
- 尚未驗證欄位不得填入假資料。

Core Tree 設定應使用：

- 客戶輸入識別：`customer-<SOURCE_SLUG>`；
- 來源 OOTB 輸入識別：`ootb-<SOURCE_SLUG>`；
- 最終 OOTB 輸入識別：`ootb-<TARGET_SLUG>`；
- 比較名稱：`customer-<SOURCE_SLUG>-To-ootb-<TARGET_SLUG>`。

六、範本檔案

在案件根目錄建立下列範本；範本只能包含路徑、欄位名稱、狀態與 `<待提供>` 佔位符：

- `core-tree\\requests\\preflight-request.template.json`
- `core-tree\\requests\\comparison-request.template.json`
- `core-tree\\inputs\\customer-<SOURCE_SLUG>\\evidence\\VERSION-EVIDENCE-TEMPLATE.md`
- `core-tree\\inputs\\ootb-<SOURCE_SLUG>\\evidence\\VERSION-EVIDENCE-TEMPLATE.md`
- `core-tree\\inputs\\ootb-<TARGET_SLUG>\\evidence\\VERSION-EVIDENCE-TEMPLATE.md`
- `package-upgrade\\pending-route\\route-input.template.json`
- `handoff\\preparation-checklist.md`

範本不得包含：

- 密碼、Token、連線字串或秘密金鑰；
- 真實 DB 名稱或帳號密碼；
- 未確認的 Build、Patch、Support 路徑或 Checksum；
- 完整客戶 Package、Core Tree 或 Log；
- `Completed`、`Approved` 或 `RolledBack` 偽造狀態。

七、人工放置資料說明

建立完成後，請只回報以下待人工動作，不要代替操作人員執行：

1. 將客戶目前版本 Core Tree 放入 `core-tree\\inputs\\customer-<SOURCE_SLUG>\\tree\\`。
2. 將同版本 OOTB Core Tree 放入 `core-tree\\inputs\\ootb-<SOURCE_SLUG>\\tree\\`。
3. 將最終版本 OOTB Core Tree 放入 `core-tree\\inputs\\ootb-<TARGET_SLUG>\\tree\\`。
4. 將三份版本證據放入各自的 `evidence\\` 目錄。
5. 由操作人員確認每份 Core Tree 均包含 `Innovator\\Client` 與 `Innovator\\Server`。
6. 由操作人員提供 Package／DB 升級路徑與各跳點官方 Patch／Support 資訊。

八、完成回報

完成後請輸出：

- 案件根目錄；
- 案件 GUID；
- 來源／最終版本；
- Core Tree 三份輸入預定路徑；
- 已建立的目錄與範本清單；
- 未填寫及待人工提供的資料；
- 是否有既有資料或安全阻擋；
- 下一個安全動作：只允許先執行 Core Tree preflight 或案件資料檢查。

禁止事項：

- 不要執行 DB、SQL、AML Import、Package Import、Aras Upgrade Tool 或 Aras Export。
- 不要自動複製、搬移或讀取客戶 Core Tree／OOTB 內容。
- 不要從資料夾名稱推測版本或 Build。
- 不要把 Core Tree 比較當成 Package／DB 升級跳點。
- 不要建立或修改正式 `Completed`、核准、Rollback 或人工確認紀錄。
```

## 芯洲範例參數

芯洲 11SP9→R38 可以填入：

```text
客戶代號：芯洲
案件目錄名稱：芯洲
案件根目錄：K:\70.ArasUpgradeCases
來源版本：11.0 SP9 Build <待提供>
來源版本短名稱：11sp9
最終版本：R38 <完整版本待提供>
最終版本短名稱：r38
```

產生後，Core Tree 的人工放置位置會是：

```text
K:\70.ArasUpgradeCases\芯洲\core-tree\inputs\customer-11sp9\tree\
K:\70.ArasUpgradeCases\芯洲\core-tree\inputs\ootb-11sp9\tree\
K:\70.ArasUpgradeCases\芯洲\core-tree\inputs\ootb-r38\tree\
```

這些資料放置完成後，下一步才是執行唯讀 Core Tree preflight；它與 Package／DB 升級準備及後續逐跳 Package Import 分開處理。

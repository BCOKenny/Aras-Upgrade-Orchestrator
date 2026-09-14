# Codex Prompt：Core Tree 輸入證據產生

本 Prompt 用於操作人員已將三份 Core Tree 複製到案件指定位置後，檢查輸入目錄並產生 Core Tree 證據文件。它不執行 Core Tree 比較、不修改 Core Tree 內容，也不執行 DB、Package Import 或 Aras 升級。

## 使用模式

```text
EVIDENCE_PREVIEW：只檢查並列出每個 Evidence Set 狀態，不寫入文件
EVIDENCE_WRITE：先完成三組零寫入預檢；只建立缺少且可安全建立的 Evidence Set，不覆寫既有文件
```

## 正式 CLI 入口

Evidence 流程必須使用已編譯的 `ArasUpgradeOrchestrator.CoreTree.Cli`，不得由 Codex 臨時組合長時間 PowerShell 寫入腳本。

- `--evidence-preview <request.json>`：讀取三組輸入並輸出各組狀態；任何未完成組回傳 exit code `2`。
- `--verify-evidence <request.json>`：重新逐筆驗證既有 `integrity.sha256`；只有三組均為 `CompleteCompatible` 才回傳 exit code `0`。
- `--evidence-write <request.json>`：先完成三組預檢；任一組為 `Partial`、`StaleOrConflict` 或 `Blocked` 時回傳 exit code `2` 且不寫入。只有 `Missing` 建立文件，`CompleteCompatible` 重用。

request 必須包含案件根目錄、操作者與剛好三組輸入，每組提供 `inputId`、`innovatorVersion`、相對於案件根目錄的 `treePath` 與 `evidencePath`。CLI 必須輸出完整 JSON 結果與程序 exit code；輸出不完整、逾時或無法取得 exit code 時，一律視為 `ExecutionUncertain`，不得宣告成功或重跑。

第一次執行建議使用 `EVIDENCE_PREVIEW`；確認預計路徑、檔案數與輸入識別正確後，再使用 `EVIDENCE_WRITE`。

## 第二次執行的簡化方式

建議在同一個 Codex task 連續執行兩階段。第一次完成 `EVIDENCE_PREVIEW` 後，不必重新貼上案件與版本參數，直接輸入：

```text
預覽結果確認無誤。請沿用上一則訊息中已驗證的全部案件、版本、操作者及三份輸入路徑，將執行模式改為 EVIDENCE_WRITE，重新執行完整檢查。只建立 Missing 的四份證據文件，重用 CompleteCompatible 的既有 Evidence Set；不得變更任何參數或覆寫既有文件。
```

第二次必須重新檢查三份 Tree 與既有證據文件，但沿用第一次已解析的參數。若第一次結果中有任何路徑、輸入識別、檔案數或 Evidence Set 狀態不正確，不能直接寫入，應先修正參數並重新預覽。

## Evidence Set 狀態與安全續行

每個輸入識別的四份文件 `version-primary.md`、`integrity.md`、`integrity.sha256`、`source-provenance.md` 合稱一個 Evidence Set。`VERSION-EVIDENCE-TEMPLATE.md` 不屬於 Evidence Set，存在時不影響狀態判定。

| 狀態 | 判定 | `EVIDENCE_WRITE` 行為 |
|---|---|---|
| `Missing` | 四份文件皆不存在。 | 可建立該組。 |
| `CompleteCompatible` | 四份文件皆存在，且 `integrity.sha256` 重新計算後與目前 Tree 的完整檔案清單相符，版本與輸入識別也相符。 | `Reused`，不得重寫。 |
| `Partial` | 只存在四份文件中的一部分。 | 阻擋，保留所有既有部分產物。 |
| `StaleOrConflict` | 四份文件皆存在，但任一輸入識別、版本資訊或完整性清單與目前 Tree 不相符。 | 阻擋，禁止覆寫。 |

`EVIDENCE_PREVIEW` 必須列出三個輸入各自的狀態、既有文件清單及預計動作。`EVIDENCE_WRITE` 必須在任何檔案寫入前，完成三個輸入的零寫入預檢。任一輸入為 `Partial` 或 `StaleOrConflict` 時，整次停止；不得建立任何其他輸入的新文件。

三個輸入均為 `Missing` 或 `CompleteCompatible` 時才可寫入。對每個 `Missing` 輸入，先在該 Evidence 目錄之外、全新的 staging 目錄建立並驗證四份文件；四份均成功且 SHA-256 清單重新核對 Tree 後，才提交到該 Evidence 目錄。提交失敗時保留 staging 與錯誤位置，不得自動刪除、覆寫或重試。

完成回報必須將每個輸入列為 `Created`、`Reused` 或 `Blocked`；不得把先前已存在的 Evidence Set 說成此次新建，也不得把部分寫入說成完成。

若必須開啟新的 Codex task，則不能依賴上一個 task 的對話內容；請重新提供完整參數，或由操作人員先保存一份案件參數檔，再以該檔案作為輸入來源。案件參數檔不得包含密碼、Token、連線字串或客戶檔案內容。

## 含 `<待提供>` 的第一次 Prompt

若版本 Build、來源位置或證據資訊尚未確認，第一次仍可使用下列 Prompt。`<待提供>` 只能表示未知資料，不得由 Codex 自行猜測或從檔名推導。

```text
請讀取並依照 docs/operations/core-tree/codex-core-tree-evidence-prompt.md 執行 Core Tree 輸入證據產生。

執行模式：EVIDENCE_PREVIEW
案件根目錄：K:\70.ArasUpgradeCases\<案件目錄>
案件識別：<案件識別>
來源版本：<來源版本，例如 11.0 SP9>
來源版本短名稱：<來源版本短名稱，例如 11sp9>
最終版本：<最終版本，例如 R38>
最終版本短名稱：<最終版本短名稱，例如 r38>
操作者：<DOMAIN\\USER>

三份輸入請依下列規則推導：
- CustomerSource：<案件根目錄>\\core-tree\\inputs\\customer-<來源版本短名稱>\\tree
- SourceOotb：<案件根目錄>\\core-tree\\inputs\\ootb-<來源版本短名稱>\\tree
- TargetOotb：<案件根目錄>\\core-tree\\inputs\\ootb-<最終版本短名稱>\\tree

請只執行預覽：確認三個 Tree 存在、互不重疊、各自包含 Innovator\\Client 與 Innovator\\Server，並列出檔案數及預計產出文件。
不要建立、修改、覆寫任何文件，也不要執行 Core Tree 比較、DB、Package Import 或升級。
版本 Build、原始來源位置、複製方式、證據截圖與人工確認仍標記為 <待提供>。
```

第一次預覽確認無誤後，在同一個 task 只輸入：

```text
預覽結果確認無誤。請沿用上一則訊息中已驗證的全部案件、版本、操作者及三份輸入路徑，將執行模式改為 EVIDENCE_WRITE，重新執行完整檢查。只建立 Missing 的四份證據文件，重用 CompleteCompatible 的既有 Evidence Set；不得變更任何參數或覆寫既有文件。
```

## 可自動產生與必須人工提供

### 可自動產生

- `integrity.sha256`：掃描每個 `tree` 目錄下的檔案，依相對路徑排序後產生 SHA-256 清單；相對路徑使用 `/`。
- `integrity.md`：填入輸入識別、Tree root、蒐集時間、蒐集人、檔案數、SHA-256 清單位置及產生方式。
- `source-provenance.md`：填入輸入識別、目的位置、複製時間、複製人員及相關證據文件位置。
- `version-primary.md`：只能填入操作人員明確提供的產品版本、輸入識別、蒐集人及時間。
- 若案件參數已明確提供 `11.0 SP9`，`version-primary.md` 可填入 `Service Pack：SP9（依案件參數提供，待證據確認）`；`Edition` 預設填入 `Enterprise（案件預設值；Aras 安裝未區分 Edition）`。若操作人員另提供不同的 Edition 定義，才替換此預設值。
- `IOM.dll` 證據：若 `<tree>\\Innovator\\Server\\bin\\IOM.dll` 存在，讀取其 `FileVersion` 與 `ProductVersion`，並將實際值及完整路徑填入 `version-primary.md` 與 `integrity.md`。這是檔案版本證據，不得直接宣告為正式 Aras Build，除非已有版本對應 SOP 或人工確認。
- Hotfix：Core Tree 未包含 Hotfix 時，只有操作人員明確確認「無 Hotfix」才填入 `None（操作人員確認）`；不能只因掃描不到檔案就推定沒有 Hotfix。
- 原始來源位置參考：若操作人員確認來源位於案件根目錄下，例如 `K:\\70.ArasUpgradeCases\\ootb-r38\\tree`，可填相對路徑 `ootb-r38\\tree`；不得只依目的目錄名稱推定來源。
- 複製或 export 方式：預設填入 `Export`；若實際使用 Windows 檔案總管複製／貼上或 `robocopy`，操作人員再替換為實際方式。
- 保管鏈參考預設填入 `source-provenance.md`、`version-primary.md`、`integrity.md`、`integrity.sha256`；若有案件／工單編號，再補充於此欄。
- 人工確認參考預設填入案件操作者，例如 `BCO\\kenny（案件操作者；確認日期與事項於執行時填寫）`；不得將此預設值視為已完成正式核准。

### 不得自行推定

- Build、Hotfix、Edition 或實際產品版本；
- 原始來源位置、複製／export 方式或保管鏈；
- 截圖、安裝證據、人工確認人員；
- 排除檔案及排除原因。

無法由參數或操作人員提供的欄位填入 `<待提供>`，不得從資料夾名稱、檔案名稱或檔案內容猜測。

## 可直接貼上的 Prompt

```text
請使用本專案的 Core Tree 輸入驗證規則，執行 Core Tree 證據產生。

執行模式：EVIDENCE_PREVIEW 或 EVIDENCE_WRITE
案件根目錄：<CASE_ROOT>
案件識別：<CASE_ID>
來源版本：<SOURCE_VERSION>
來源版本短名稱：<SOURCE_SLUG>
最終版本：<TARGET_VERSION>
最終版本短名稱：<TARGET_SLUG>
操作者：<DOMAIN\\USER>

三份輸入：
1. CustomerSource
   輸入識別：customer-<SOURCE_SLUG>
   Tree：<CASE_ROOT>\\core-tree\\inputs\\customer-<SOURCE_SLUG>\\tree
   Evidence：<CASE_ROOT>\\core-tree\\inputs\\customer-<SOURCE_SLUG>\\evidence

2. SourceOotb
   輸入識別：ootb-<SOURCE_SLUG>
   Tree：<CASE_ROOT>\\core-tree\\inputs\\ootb-<SOURCE_SLUG>\\tree
   Evidence：<CASE_ROOT>\\core-tree\\inputs\\ootb-<SOURCE_SLUG>\\evidence

3. TargetOotb
   輸入識別：ootb-<TARGET_SLUG>
   Tree：<CASE_ROOT>\\core-tree\\inputs\\ootb-<TARGET_SLUG>\\tree
   Evidence：<CASE_ROOT>\\core-tree\\inputs\\ootb-<TARGET_SLUG>\\evidence

版本證據模板來源：
docs/operations/core-tree/templates/version-primary.template.md
docs/operations/core-tree/templates/integrity.template.sha256
docs/operations/core-tree/templates/source-provenance.template.md

請依下列順序執行：
1. 先確認三個 Tree 目錄存在，且互不重疊。
2. 確認每個 Tree 下存在 Innovator\\Client 與 Innovator\\Server。
3. 只讀取 Tree 的檔案清單與檔案大小；不得修改、搬移或重新整理 Tree 內容。
4. 讀取三個 Tree 各自的 `Innovator\\Server\\bin\\IOM.dll` `FileVersion`／`ProductVersion`；不存在時填入 `<待提供>` 並回報。
5. 在預覽結果列出每個輸入的檔案數、Tree 完整路徑、Evidence 完整路徑、Evidence Set 狀態、既有文件及預計動作。
6. EVIDENCE_PREVIEW 只輸出預覽，不建立或修改文件。
7. EVIDENCE_WRITE 必須先完成三組 Evidence Set 零寫入預檢。只有 `Missing` 可進入 staging；`CompleteCompatible` 必須重用。
8. 每個 `Missing` 輸入在全新的 staging 目錄使用模板建立並驗證下列四份文件，完成後才提交到 Evidence 目錄：
   - version-primary.md
   - integrity.md
   - integrity.sha256
   - source-provenance.md
9. `Partial` 或 `StaleOrConflict` 必須阻擋整次寫入；不得覆寫、刪除、合併或建立其他輸入的新文件。
10. integrity.sha256 必須重新依 Tree 實際檔案產生，不可複製其他案件的 Checksum。
11. 產生完成後回報每個輸入的 `Created`／`Reused`／`Blocked`、文件完整路徑、檔案數、SHA-256 清單筆數、IOM.dll 版本及 `<待提供>` 欄位。

PowerShell 執行規則：
- 路徑組合使用 Join-Path -Path -ChildPath。
- 路徑檢查使用 Test-Path -LiteralPath。
- 建立目錄使用 [System.IO.Directory]::CreateDirectory(<完整路徑>)。
- 不使用 New-Item -LiteralPath。
- 不以資料夾名稱推定版本或 Build。
- 必須相容 Windows PowerShell 5.1 與其 .NET Framework 執行環境；不得使用 [System.IO.Path]::GetRelativePath()，因為該 API 不保證存在。
- 若任何檢查失敗，停止並回報，不自行改寫指令後重試。

計算 Tree 內檔案的相對路徑時，僅可對已驗證位於該 Tree 之下的檔案使用下列相容函式。此函式先做包含範圍檢查，再移除固定 Tree 前綴；不得用它處理 Tree 外的路徑或推導 `..` 路徑。

```powershell
function Get-TreeRelativePathCompat {
    param(
        [string]$TreePath,
        [string]$FilePath
    )

    $treeFull = [System.IO.Path]::GetFullPath($TreePath).TrimEnd([char]92)
    $fileFull = [System.IO.Path]::GetFullPath($FilePath)
    $treePrefix = $treeFull + [char]92

    if (-not $fileFull.StartsWith($treePrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'The file path is outside the verified Tree path.'
    }

    return $fileFull.Substring($treePrefix.Length).Replace([char]92, [char]47)
}
```

PowerShell 腳本穩定性規則：
- 不要把完整多行腳本拼成 `powershell -Command` 的單行字串；應先產生暫存 `.ps1` 腳本。
- 產生腳本後先做語法解析；解析失敗時不得進行任何寫入。
- 腳本必須設定 `$ErrorActionPreference = 'Stop'`，並以 `try/catch` 回報失敗位置。
- 腳本不得呼叫 `[System.IO.Path]::GetRelativePath()`；相對路徑一律使用 `Get-TreeRelativePathCompat`，並只傳入已通過 Tree 範圍檢查的檔案。
- 路徑與輸入資料以變數傳入，不要把含空白、反斜線或中文的路徑直接嵌入未加引號的腳本片段。
- `.ps1` 必須以 UTF-8 with BOM 寫入，且可執行腳本來源限 ASCII；含中文的案件名稱或路徑片段以 Unicode code point 在執行期重建，不直接放入腳本來源或命令列。
- 所有三組 Evidence Set 必須先完成零寫入預檢；只有 `Missing` 可建立 staging。任一輸入為 `Partial` 或 `StaleOrConflict` 時，整次停止。編碼或提交失敗後不得自動刪除部分產物或自動重試，必須先列出完整路徑並取得操作人員明確核准。
- 語法解析通過後只執行一次；若執行失敗，保留錯誤與部分產出清單，不能自動改寫語法重跑。
- 腳本必須以 `try/catch` 包住主要流程；`catch` 必須輸出穩定錯誤代碼、失敗階段、目前輸入識別、完整路徑、例外訊息及已建立／未建立清單，並以非零退出碼結束。
- 腳本結束時必須明確輸出成功摘要或失敗摘要；執行器不得以空白輸出或只有逾時訊息推定成功。若無法取得程序退出碼或輸出不完整，結果標記為 `ExecutionUncertain`，停止後續提交。
- SHA-256 掃描大型 Tree 時，至少在開始、每個輸入完成及每 1,000 個檔案輸出一次進度，包含輸入識別、已處理檔案數、總檔案數及目前階段；不得輸出客戶檔案內容。
- 每組 staging 建立四份文件後，必須輸出 staging 完整路徑、四份文件清單、檔案數及 SHA-256 清單筆數；提交後再次輸出四份目的文件完整路徑及驗證結果。
- 提交前必須重新掃描目前 Tree 並核對 `integrity.sha256` 的檔案數、相對路徑與 SHA-256；核對失敗時不得提交該組，也不得刪除 staging。
- 執行結果必須區分：規則／輸入阻擋（`Blocked`）、腳本明確失敗（`Failed`）、程序逾時或退出狀態不明（`ExecutionUncertain`），以及全部驗證與提交成功（`Completed`）。`ExecutionUncertain` 不得回報為 `Created`。

Windows 路徑若經 JavaScript 傳遞給 PowerShell，不得使用 JavaScript template literal、`String.raw` 或任何反引號；本地編排層可能再次解析它們。只能將每個反斜線寫成 `\\` 的普通 JavaScript 字串，例如 `"K:\\70.ArasUpgradeCases"`。Markdown 行內程式碼標記、程式碼圍欄與對話原文不得拼入執行命令。優先使用已完成語法解析的暫存 `.ps1` 腳本，並以 `powershell -File` 執行。

禁止事項：
- 不執行 Core Tree preflight、比較、A／B／C 分類或交付。
- 不執行 DB、SQL、Package Import、AML Import、Aras Export 或升級工具。
- 不修改 Tree 內任何檔案。
- 不覆寫既有證據文件。
```

## 芯洲案件範例

```text
執行模式：EVIDENCE_PREVIEW
案件根目錄：K:\70.ArasUpgradeCases\Customer1209-To-R38
案件識別：Customer1209-To-R38
來源版本：12.0 SP9
來源版本短名稱：12sp9
最終版本：R38
最終版本短名稱：r38
操作者：BCO\kenny
```

預覽確認無誤後，僅將執行模式改為：

```text
執行模式：EVIDENCE_WRITE
```

此流程的輸出是 Core Tree 輸入證據，不能取代版本證明、官方 SOP、Package／DB 升級證據或人工確認。

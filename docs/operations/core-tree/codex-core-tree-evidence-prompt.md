# Codex Prompt：Core Tree 輸入證據產生

本 Prompt 用於操作人員已將三份 Core Tree 複製到案件指定位置後，檢查輸入目錄並產生 Core Tree 證據文件。它不執行 Core Tree 比較、不修改 Core Tree 內容，也不執行 DB、Package Import 或 Aras 升級。

## 使用模式

```text
EVIDENCE_PREVIEW：只檢查並列出預計產出，不寫入文件
EVIDENCE_WRITE：檢查通過後建立證據文件，不覆寫既有文件
```

第一次執行建議使用 `EVIDENCE_PREVIEW`；確認預計路徑、檔案數與輸入識別正確後，再使用 `EVIDENCE_WRITE`。

## 第二次執行的簡化方式

建議在同一個 Codex task 連續執行兩階段。第一次完成 `EVIDENCE_PREVIEW` 後，不必重新貼上案件與版本參數，直接輸入：

```text
預覽結果確認無誤。請沿用上一則訊息中已驗證的全部案件、版本、操作者及三份輸入路徑，將執行模式改為 EVIDENCE_WRITE，重新執行完整檢查後產生四份證據文件。不得變更任何參數，不得覆寫既有文件。
```

第二次必須重新檢查三份 Tree 與既有證據文件，但沿用第一次已解析的參數。若第一次結果中有任何路徑、輸入識別或檔案數不正確，不能直接寫入，應先修正參數並重新預覽。

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
預覽結果確認無誤。請沿用上一則訊息中已驗證的全部案件、版本、操作者及三份輸入路徑，將執行模式改為 EVIDENCE_WRITE，重新執行完整檢查後產生四份證據文件。不得變更任何參數，不得覆寫既有文件。
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
5. 在預覽結果列出每個輸入的檔案數、Tree 完整路徑、Evidence 完整路徑及預計寫入文件。
6. EVIDENCE_PREVIEW 只輸出預覽，不建立或修改文件。
7. EVIDENCE_WRITE 通過全部檢查後，使用模板建立每個 Evidence 目錄中的：
   - version-primary.md
   - integrity.md
   - integrity.sha256
   - source-provenance.md
8. 既有上述文件時停止該輸入並回報，不覆寫、不刪除、不合併。
9. integrity.sha256 必須重新依 Tree 實際檔案產生，不可複製其他案件的 Checksum。
10. 產生完成後回報每個文件的完整路徑、檔案數、SHA-256 清單筆數、IOM.dll 版本及 `<待提供>` 欄位。

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
- 所有預定輸出必須先完成零寫入預檢；編碼失敗後不得自動刪除部分產物或自動重試，必須先列出完整路徑並取得操作人員明確核准。
- 語法解析通過後只執行一次；若執行失敗，保留錯誤與部分產出清單，不能自動改寫語法重跑。

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

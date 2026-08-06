# Core Tree 細項能力 Skill 架構設計

狀態：需求方已核准
日期：2026-08-04

## 1. 背景與目的

目前 `aras-compare-core-tree` 是比較及分類 Core Tree 的功能 Skill，正式規則主要對應既有 C# 核心。需求方希望公司同事能以更細且明確的 Skill 了解、直接執行及維護每一項穩定業務能力，並讓未來 AI 能依相同規格建立 C#、Python 或其他語言實作。

本設計以 Core Tree 為第一個試點，將功能拆成五個可獨立呼叫、亦可由父 Skill 組合的細項能力 Skill。Skill 是業務契約及驗收規格來源；程式是必須通過相同驗收案例的可替換實作。

## 2. 設計目標

- 依穩定業務能力拆分 Skill，不依目前 `.cs` 類別或函式拆分。
- 每個細項 Skill 都能由公司同事直接要求執行。
- `aras-compare-core-tree` 保留完整流程協調責任。
- 每個細項 Skill 具備完整輸入、輸出、規則、錯誤、停止及驗收契約。
- 驗收資產不依賴實作語言；不同語言實作必須產生相同業務結果。
- 保留既有 C# 實作，將其定位為第一個符合契約的參考實作。
- Core Tree 試點完成後，再決定是否將相同架構擴展至 Package、Rule 1／Rule 2 或升級跳點。

## 3. 非目標

- 本設計不立即建立五個 Skill。
- 本設計不修改、刪除或改寫現有 C# 核心。
- 本設計不改變既有 Core Tree A／B／C 業務規則。
- 本設計不合併或修改 R38 Core Tree。
- 本設計不將讀檔、Hash、排序、JSON 序列化或目錄走訪等技術細節建立成 Skill。
- 本設計不自動拆分其他 Aras 升級領域的 Skill。

## 4. 架構決策

採用「五個獨立 Skill＋父 Skill 協調」：

```text
aras-compare-core-tree
│
├─ aras-validate-core-tree-inputs
├─ aras-compare-core-tree-content
├─ aras-resolve-core-tree-file-mappings
├─ aras-classify-core-tree-differences
└─ aras-build-core-tree-delivery
```

父 Skill 接收完整 Core Tree 比較需求、選擇及組合細項能力、彙整狀態與證據。細項 Skill 擁有各自的能力契約，可被父 Skill 呼叫，也可由同事直接呼叫。

五個 Skill 並非固定各執行一次。分類能力會依客戶檔案重複使用內容比較及邏輯檔案對應能力：

```text
aras-compare-core-tree
│
├─ 驗證三份輸入
│
├─ 執行 A／B／C 分類
│  ├─ 重複判定兩個檔案內容是否相同
│  └─ 重複解析目標版邏輯檔案
│
└─ 建立交付目錄
```

## 5. 建立獨立 Skill 的判定標準

執行單元必須同時符合下列條件，才建立獨立 Skill：

- 代表穩定的業務能力。
- 公司同事有單獨提出該任務的需要。
- 具有完整輸入與輸出契約。
- 具有獨立錯誤及停止條件。
- 可以用語言中立案例獨立驗收。
- 可直接執行，也可被上層 Skill 組合。

技術實作單元不因存在類別或函式就建立 Skill。讀取檔案、計算 Checksum、排序、序列化及目錄走訪等機制，除非日後形成可獨立要求且有完整業務契約的能力，否則留在實作層。

## 6. 共用 Skill 骨架

每個細項 Skill 採相同結構：

```text
<skill-name>/
├─ SKILL.md
├─ references/
│  ├─ input-contract.md
│  ├─ output-contract.md
│  ├─ rules.md
│  └─ error-and-stop-conditions.md
└─ assets/
   └─ acceptance-cases/
      ├─ case-001/
      │  ├─ input/
      │  └─ expected/
      └─ case-002/
```

`SKILL.md` 必須包含：

- 使用時機與不適用範圍。
- 同事可直接使用的中文指令範例。
- 前置條件。
- 執行步驟。
- 完成條件。
- 人工確認點。
- 失敗、停止及安全重試規則。
- 與父 Skill、前後細項 Skill 的交接方式。

每個 Skill 必須提供：

- 明確的輸入契約。
- 明確的輸出契約。
- 不受程式語言影響的固定規則。
- 錯誤與停止條件。
- 代表性輸入及對應預期結果。
- 可明確判定 Pass／Fail 的驗收標準。

結構化結果建議使用 JSON，供程式或 AI 交換；同時產生中文 Markdown 摘要供同事閱讀。契約應固定結果欄位、狀態、錯誤代碼、相對路徑、來源證據及規則版本，不固定 JSON Property 的排列順序。

共用術語集中於 Core Tree 共用術語文件，細項 Skill 引用同一份定義，避免「來源版 OOTB」「目標版 OOTB」「邏輯同一檔案」或「人工確認」在各 Skill 中產生不同解釋。

## 7. 細項 Skill 責任

### 7.1 `aras-validate-core-tree-inputs`

責任：驗證三份 Core Tree 及比較執行所需證據是否完整、安全且一致。

輸入：

- 客戶來源版 Core Tree。
- 相同版本 OOTB Core Tree。
- 目標版 OOTB Core Tree。
- 三份版本證據。
- Server 文字比較規則版本及 Checksum。
- 不存在的新輸出嘗試目錄。

輸出：

- 驗證通過的輸入清單及證據；或
- 明確的阻擋原因及穩定錯誤代碼。

邊界：不比較內容、不解析邏輯檔案、不分類、不複製檔案。

直接要求範例：「驗證這三份 Core Tree 是否可以開始比較。」

### 7.2 `aras-compare-core-tree-content`

責任：依 Core Tree Client／Server 規則，判定兩個指定檔案在業務上是否視為相同。

輸入：

- 左、右兩個檔案。
- Core Tree 相對路徑。
- Client／Server 身分。
- Server 文字比較規則版本及 Checksum。

輸出：

- `Equal` 或 `Different`。
- 實際採用的文字、二進位或解碼失敗後二進位比較模式。
- 規則版本、技術提示及證據。

固定規則：

- Client 指定文字類型只忽略 CRLF／LF 與 UTF BOM。
- 空白、縮排、大小寫及其他內容差異仍視為修改。
- Server 只有版本化規則集明確指定的相對路徑採文字比較。
- 其他檔案採完整串流二進位比較；無法可靠解碼時改採二進位並記錄原因。

邊界：不掃描整棵樹、不解析目標檔案、不判定 A／B／C。

直接要求範例：「依 Core Tree 規則比較這兩個檔案是否相同。」

### 7.3 `aras-resolve-core-tree-file-mappings`

責任：在目標版 OOTB Core Tree 中解析來源檔案的唯一邏輯對應。

輸入：

- 來源相對路徑。
- 目標版 OOTB Core Tree。
- 允許的副檔名演進規則。

輸出：

- `None`：沒有對應。
- `Unique`：唯一對應。
- `Ambiguous`：多個候選，必須人工確認。
- 套用的演進規則與所有候選相對路徑。

第一版允許的演進包含：

- `htm → html`
- `htm → cshtml`
- `html → cshtml`
- `js → ts`
- `js → tsx`

固定限制：只在相同相對目錄及相同主檔名內解析，不跨目錄搜尋，不以版本名稱預先建立固定對應表，多候選不得猜測。

邊界：不比較內容、不判定分類、不複製或改名來源檔案。

直接要求範例：「找出這個舊版 js 檔在目標 Core Tree 中的邏輯對應。」

### 7.4 `aras-classify-core-tree-differences`

責任：掃描客戶 Core Tree，使用內容比較及邏輯檔案對應能力，產生 A／B／C、人工確認、錯誤及提示決策。

分類契約：

- 客戶存在、來源 OOTB 不存在：A。
- A 類與目標版邏輯檔案碰撞：人工確認，不建立 A。
- 客戶與來源 OOTB 相同：不交付。
- 客戶與來源 OOTB 不同，目標版沒有唯一邏輯對應：B。
- 客戶與來源 OOTB 不同，目標版有唯一邏輯對應：C。
- 目標版存在多個候選：人工確認，不建立分類 D。

輸出：

- 穩定排序的 A／B／C 分類項目。
- 待人工確認清單。
- 錯誤及技術提示清單。
- 規則版本、輸入證據及整體 `ReadyToComplete` 或 `Blocked` 狀態。

邊界：不建立 A／B／C 目錄、不修改三份輸入、不標記 `Completed`。

直接要求範例：「只分類這三份 Core Tree，不建立交付目錄。」

### 7.5 `aras-build-core-tree-delivery`

責任：依已驗證分類結果，在新的執行嘗試目錄建立可交接的 Core Tree 比較產出。

輸入：

- 已完成的分類結果。
- 三份已驗證輸入及證據。
- 不存在的新嘗試目錄。

輸出目錄：

- A：只含 `CustomerSource`。
- B：含 `CustomerSource` 與 `OOTBSource`。
- C：含 `CustomerSource`、`OOTBSource` 與 `OOTBR38`。

輸出亦包含分類摘要、人工確認、錯誤、Checksum、規則版本及 `Incomplete`／`Completed` 狀態。

固定規則：

- C 類 `CustomerSource` 及 `OOTBSource` 複本使用目標版檔名與副檔名，但不轉換內容。
- A、B 保留來源檔名。
- 任一錯誤或待人工確認存在時，只能產生 `Incomplete`。
- 只有零錯誤且零人工確認才能建立 `Completed`。
- 每次重試建立新的輸出嘗試，不覆寫舊結果。

邊界：不重新分類、不修改任何輸入、不合併或修改 R38 Core Tree。

直接要求範例：「使用這份已確認分類結果建立 Core Tree 比較交付目錄。」

## 8. 穩定結果、提示、人工確認、錯誤與狀態契約

各類代碼屬於語言中立契約，實作不得要求父 Skill 解析特定語言的 Exception 文字。第一版至少區分：

| 類型 | 代碼或值 |
|---|---|
| 配對結果 | `None`、`Unique`、`Ambiguous` |
| 內容結果 | `Equal`、`Different` |
| 技術提示 | `TextDecodeFallback` |
| 人工確認 | `MultipleTargetMappings`、`CustomerAdditionCollidesWithTarget` |
| 阻擋錯誤 | `InputDirectoryMissing`、`VersionEvidenceMismatch`、`RequiredTreeStructureMissing`、`InputOutputOverlap`、`RuleChecksumMismatch`、`OutputAttemptAlreadyExists` |
| 分類狀態 | `ReadyToComplete`、`Blocked` |
| 交付狀態 | `Incomplete`、`Completed` |

`NoTargetMapping` 不視為錯誤；它由配對結果 `None` 表示，並依分類上下文形成 A 或 B。`OutputIncomplete` 不另設錯誤代碼；它由交付狀態 `Incomplete` 表示，並附實際錯誤或待人工確認原因。

實作可以提供更詳細的中文訊息，但類型、代碼、必要欄位及業務狀態必須穩定。

停止及局部繼續規則：

- 輸入或版本驗證失敗：整體停止，不開始比較。
- 單一檔案讀取失敗：記錄錯誤，其餘檔案可以繼續分析，但整體不得完成。
- 找到多個目標候選：該檔案停止分類，其他檔案可以繼續。
- 取消、失敗或中斷：保留診斷成果並標記 `Incomplete`。
- 存在任何錯誤或待人工確認：不得產生 `Completed`。
- 重試必須建立新執行嘗試，不覆寫既有結果。

## 9. 跨語言驗收契約

每個驗收案例必須記錄：

- 案例目的。
- 輸入檔案與輸入清單。
- 固定規則版本。
- 預期結構化結果。
- 預期產出目錄與檔案。
- 預期狀態。
- 不得發生的副作用。

驗收比較規則：

- 比較結構化結果語意，不要求 JSON Property 排列順序相同。
- 分類、相對路徑、狀態及錯誤代碼必須完全一致。
- 結果清單必須依相對路徑穩定排序。
- 交付檔案內容及 Checksum 必須一致。
- 三份輸入 Core Tree 必須保持不變。
- C#、Python 或其他語言必須通過同一組案例。

最低案例矩陣：

| Skill | 必須涵蓋的案例 |
|---|---|
| `aras-validate-core-tree-inputs` | 三份正確輸入、版本不符、缺少 Client／Server、輸出與輸入重疊、規則 Checksum 不符 |
| `aras-compare-core-tree-content` | CRLF／LF、UTF BOM、空白差異、Server 指定文字檔、其他 Server 檔案二進位比較、無法解碼 fallback |
| `aras-resolve-core-tree-file-mappings` | 完全同名、五種副檔名演進、不存在、多候選、禁止跨目錄配對 |
| `aras-classify-core-tree-differences` | A、B、C、未修改不交付、A 與目標碰撞、多候選人工確認、單一檔案讀取錯誤 |
| `aras-build-core-tree-delivery` | A／B／C 目錄、C 改用目標副檔名但內容不轉換、來源不變、新嘗試、禁止覆寫、`Incomplete`／`Completed` |

建立或修改任一 Skill 前，必須依 `superpowers:writing-skills` 執行 Skill 的 RED／GREEN／REFACTOR 驗證；每個 Skill 分別完成基線失敗情境、最小 Skill、重測及漏洞修正，不一次建立後才整批驗證。

## 10. 與 AML 共用標準的關係

本試點涉及 Core Tree 比較，因此所有後續工作仍須先閱讀 `docs/standards/AML_Structure_and_Traversal_Standard.md`。

目前 Core Tree 檔案比較採文字或二進位內容規則，不將 Core Tree XML 當作 Package AML 做 AML 節點語意比較，也不以一般 XML 正規化取代既有內容契約。若未來新增 AML 語意解析、比較、複製或修改，必須完整區分 AML Root、Item、Scalar Property、Item Property、Relationships Container 及 Relationship Item，並依標準無固定深度遞迴；若規格產生衝突，先停止並提出衝突，不得自行降低標準。

## 11. ADR 與 Skill Map 調整

正式導入時：

1. 保留 ADR 0002 的歷史背景。
2. 更新 ADR 0002 狀態或補充說明，指向新的細項能力 Skill ADR。
3. 新增 ADR，記錄穩定業務能力可以建立獨立 Skill、Skill 是契約及驗收來源、程式語言不決定 Skill 邊界，以及新實作的符合性關卡。
4. 在 `docs/design/skill-map.md` 保留 `aras-compare-core-tree`，新增五個細項 Skill 的輸入、輸出、安全關卡、相依、驗收資產及符合契約的實作。
5. 更新父 Skill 路由與既有 Core Tree 能力參考，避免將 C# 類別誤寫成唯一規格來源。

## 12. 現有 C# 實作的定位

現有 `CoreTreeInputValidator`、`CoreTreeContentComparer`、`CoreTreeLogicalPathResolver`、`CoreTreeComparisonEngine` 及 `CoreTreeComparisonBuilder` 不刪除，也不要求一個類別對應一個 Skill。

現有 C# 核心定位為第一個參考實作。AI 可以依 Skill 產生 Python 或其他語言版本，但新實作只有在符合下列條件後才能登錄為正式可用：

1. 實作相同輸入及輸出契約。
2. 使用相同狀態及錯誤代碼。
3. 通過相同驗收案例。
4. 證明輸入不變並符合安全及重試規則。
5. 將實作版本、驗收結果及 Checksum 納入可追溯證據。

AI 臨時產生但尚未通過驗收的程式，不得直接用於正式客戶升級。

## 13. 導入順序

1. 調整 ADR 與 Skill Map。
2. 建立 Core Tree 共用術語及語言中立契約。
3. 依 RED／GREEN／REFACTOR 逐一建立及驗證五個細項 Skill。
4. 更新父 Skill 的路由、直接呼叫範例及交接關係。
5. 建立或整理跨語言驗收資產。
6. 驗證現有 C# 實作符合新契約並登錄為參考實作。
7. 完成 Core Tree 試點檢討，再評估其他升級領域。

## 14. 已選方案與未選方案

已選：五個獨立 Skill＋父 Skill 協調。此方案最符合直接呼叫、獨立維護、AI 發現、語言中立規格及跨語言重建需求。

未選方案：

- 將五項能力只放在父 Skill 的 `references`：頂層 Skill 少，但同事及 AI 不容易直接發現及指定細項能力。
- 建立單一能力入口並以 `mode` 選擇：入口少，但責任及驗收範圍容易膨脹，日後維護界線不清。

## 15. 核准紀錄

需求方已依序核准：

- 五個穩定業務能力的拆分及名稱。
- 每個 Skill 的共同規格骨架與交接方式。
- 五個 Skill 的責任邊界及呼叫關係。
- 跨語言驗收、錯誤代碼與停止規則。
- ADR、Skill Map、現有程式定位及 Core Tree 試點導入策略。

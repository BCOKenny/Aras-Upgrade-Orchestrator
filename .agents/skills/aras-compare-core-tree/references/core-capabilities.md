# Core Tree 能力對照

細項 Skill 與其共同驗收案例定義業務契約。下表的程式僅是目前參考實作；C#、Python 或其他實作只有通過 `core-tree-capabilities/1` 共同驗收案例後，才能登錄為可替換的正式實作。

| 業務能力契約 | 細項 Skill | 目前參考實作 | 狀態 |
|---|---|---|---|
| 驗證三份 Core Tree、版本與輸出隔離 | `aras-validate-core-tree-inputs` | `CoreTreeInputValidator` | 試點已建立；共同驗收案例是唯一符合性依據。 |
| 依已固定規則比較兩個 Core Tree 檔案 | `aras-compare-core-tree-content` | `CoreTreeContentComparer` | 試點已建立；共同驗收案例是唯一符合性依據。 |
| 解析來源檔案在 R38 的邏輯對應 | `aras-resolve-core-tree-file-mappings` | `CoreTreeLogicalPathResolver` | 試點已建立；共同驗收案例是唯一符合性依據。 |
| 將已驗證輸入分類為 A／B／C 或阻擋 | `aras-classify-core-tree-differences` | `CoreTreeComparisonEngine` | 試點已建立；共同驗收案例是唯一符合性依據。 |
| 依已完成分類建立不可覆寫的交付 | `aras-build-core-tree-delivery` | `CoreTreeComparisonBuilder`、`DirectoryLeaseManager` | 試點已建立；共同驗收案例是唯一符合性依據。 |

目前參考實作的位置：

- `CoreTreeInputValidator`: `src/ArasUpgradeOrchestrator.Core/CoreTrees/CoreTreeInputValidator.cs`
- `CoreTreeContentComparer`: `src/ArasUpgradeOrchestrator.Core/CoreTrees/CoreTreeContentComparer.cs`
- `CoreTreeLogicalPathResolver`: `src/ArasUpgradeOrchestrator.Core/CoreTrees/CoreTreeLogicalPathResolver.cs`
- `CoreTreeComparisonEngine`: `src/ArasUpgradeOrchestrator.Core/CoreTrees/CoreTreeComparisonEngine.cs`
- `CoreTreeComparisonBuilder`: `src/ArasUpgradeOrchestrator.Core/CoreTrees/CoreTreeComparisonBuilder.cs`
- `DirectoryLeaseManager`: `src/ArasUpgradeOrchestrator.Core/Safety/DirectoryLeaseManager.cs`

## C# 符合性紀錄

- Implementation: `ArasUpgradeOrchestrator.Core/CoreTrees`
- Contract: `core-tree-capabilities/1`
- Test: `dotnet run --project tests\ArasUpgradeOrchestrator.Core.Tests -c Release`
- Conformance: `c7a88e54835fcf858fa0b1059070e1a1648d519a` (81/81 tests passed; 33 fixture pairs exercised)

此處不記錄各子 Skill 的錯誤代碼表、fixture schema 或詳細規則；需要時讀取被路由子 Skill 的契約與資產。

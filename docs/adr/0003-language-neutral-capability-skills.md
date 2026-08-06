# 0003：語言中立細項能力 Skill 架構

狀態：已採用
日期：2026-08-04

## 背景

Core Tree 比較已具備正式 C# 核心，但同事需要可直接要求、可獨立驗收的穩定業務能力；未來實作亦可能使用 C# 以外的語言。Skill 邊界必須依完整輸入、輸出、安全與驗收契約決定，而非現有類別或小型技術函式。

## 決策

採用五個獨立 Skill＋父 `aras-compare-core-tree` 協調：

- `aras-validate-core-tree-inputs`
- `aras-compare-core-tree-content`
- `aras-resolve-core-tree-file-mappings`
- `aras-classify-core-tree-differences`
- `aras-build-core-tree-delivery`

細項能力 Skill 可由同事直接要求，也可由父 Skill 路由及組合。Skill 是語言中立的契約與驗收來源；現有 C# 核心只作為第一個參考實作，並不取代契約。不依程式語言或類別切分 Skill。

## 未選方案

- 將五項能力僅置於父 Skill 的 `references`：不利直接發現、指定及獨立驗收。
- 建立單一入口並以 `mode` 選擇：責任、停止條件及驗收範圍會膨脹，無法維持穩定邊界。

## 後果

每項 Skill 都必須有完整輸入、輸出、安全、錯誤、停止與驗收契約，並以獨立 RED／GREEN／REFACTOR 證據導入。所有正式實作都必須通過相同的語言中立驗收案例、證明輸入不變並保存版本與驗收證據後，才能正式使用。Core Tree 是唯一試點；本決策不拆分 Package、Rule 1／Rule 2 或升級跳點 Skill。

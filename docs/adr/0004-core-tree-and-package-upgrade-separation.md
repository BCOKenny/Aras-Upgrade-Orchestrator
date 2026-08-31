# 0004：Core Tree 版本比較與 Package／DB 跳點分離

狀態：已採用
日期：2026-08-28

## 背景

Core Tree 與 Package 升級處理的是不同問題：Core Tree 用來辨識客戶目前版本相對於同版 OOTB 的客製差異，並判斷這些差異在最終版本是否有對應檔案；Package 升級則依 Aras Innovator 官方 Patch 與 DB 升級路徑，逐跳執行 Package Import。

若將 Core Tree 當成每個 DB 跳點都要執行的流程，會產生不必要的中間比較、混淆輸入與產出，也可能誤以為 Core Tree 分類可以取代官方 Patch 或 DB 驗證。

## 決策

1. Core Tree 採一次性的版本差異比較，輸入為客戶目前版本 Core Tree、同版本 OOTB Core Tree 及最終版本 OOTB Core Tree。
2. Core Tree 不屬於升級路徑，不依 DB／Package 中間跳點建立比較任務。
3. Package／DB 升級採有序的 Package Import 跳點；每一跳使用與來源／目標版本相符的 Aras 官方 Patch／Support，並保存獨立執行與驗證證據。
4. 案件模型分別保存 `coreTreeComparison` 與 `packageUpgradeRoute`；兩者可平行準備，但 DB 跳點本身不得平行執行。
5. Core Tree 產出與 Package Import 產出使用不同目錄、任務識別與完成條件。

## 影響

- Core Tree 執行入口不得寫死 `customer-12sp9`，而要依案件的 `coreTreeComparison` 設定解析輸入。
- `customer-11sp9`、`ootb-11sp9`、`ootb-r38` 可作為芯洲 Core Tree 三份輸入。
- `Rule 1`／`Rule 2` 的跳點差異只服務 Package Import 跳點，不修改 Core Tree 流程。
- Package Import、DB 備份、登入驗證與 Rollback 仍由操作人員依 SOP 執行；協調工具只管理關卡與證據。
- 既有只描述 `routes` 的案件可讀取，但新案件應追加 `coreTreeComparison` 設定；既有歷程不得改寫。

## 不採用方案

- 每個 Package／DB 跳點都重新比較一次 Core Tree：產出重複且無法代表最終版本客製差異。
- 用 Core Tree A／B／C 分類結果直接產生或取代官方 Package Patch：責任、版本與 DB 相依性不同，風險不可接受。

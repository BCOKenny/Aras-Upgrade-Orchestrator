# Core Tree 比較及分類設計

狀態：已由既有需求規格確認
日期：2026-08-04

## 目標與邊界

建立離線、可測試的 Core Tree 正式核心，驗證客戶來源、相同版本 OOTB 與 R38 OOTB 三份輸入後，比較 `Innovator\Client` 與 `Innovator\Server`，產生 A／B／C 分類、待人工確認清單、摘要及不可覆寫的 `Incomplete`／`Completed` 標記。本階段不合併或修改 R38、不操作正式目錄，也不建立 UI／CLI。

## 架構

- `CoreTreeInputValidator`：驗證三份根目錄、版本證據、Client／Server 結構與輸出隔離。
- `CoreTreeContentComparer`：Client 指定文字副檔名只忽略 CRLF／LF 與 UTF BOM；Server 僅依固定規則集指定相對路徑做文字比較，其餘採串流二進位比較。
- `CoreTreeLogicalPathResolver`：只在相同相對目錄及相同主檔名套用允許的副檔名演進；零候選與多候選不猜測。
- `CoreTreeComparisonEngine`：掃描三棵樹並建立 A／B／C 或人工確認決策，不寫入輸出。
- `CoreTreeComparisonBuilder`：要求不存在的新嘗試目錄、取得目錄租約、複製分類檔案，寫入摘要與狀態標記。只有零錯誤且零人工確認才建立 `Completed`。

## 分類與輸出

- A：客戶存在、來源 OOTB 不存在；與 R38 邏輯檔案碰撞時轉人工確認。
- B：客戶與來源 OOTB 不同，R38 沒有唯一邏輯對應。
- C：客戶與來源 OOTB 不同，R38 有唯一邏輯對應。
- 客戶與來源 OOTB 相同者不交付。
- C 的 CustomerSource 與 OOTBSource 複本改用 R38 檔名；內容不轉換。A／B 保留原檔名。
- 多候選不建立分類 D，也不複製歧義檔案。

## 證據與安全

請求固定案件版本、三份版本證據、Server 文字比較規則集版本與 Checksum。資料夾名稱不能取代證據。三份輸入與輸出不得相同或上下層重疊；舊嘗試不得覆寫。原始樹只讀，所有寫入集中在新輸出目錄並受 `DirectoryLeaseManager` 保護。

## 驗收

測試涵蓋輸入證據、Client／Server 文字與二進位比較、副檔名演進、多候選、A／B／C 目錄、來源不變、新嘗試、`Incomplete`／`Completed` 及 Skill 正式能力引用。

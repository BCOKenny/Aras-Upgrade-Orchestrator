# 12 Core Tree 比較及分類

Type: task
Status: resolved

## 目標

建立三份輸入及版本證據驗證、Client／Server 內容比較、相對路徑與副檔名演進、A／B／C、人工確認、新嘗試產出、目錄鎖與完成標記核心，並同步建立 `aras-compare-core-tree`。

## 驗收結果

- 三份輸入必須與案件來源／目標版本一致，且包含 `Innovator\Client` 與 `Innovator\Server`。
- Client 指定文字類型只忽略換行與 BOM；Server 只依固定版本規則做文字比較，其餘完整串流二進位比較。
- 邏輯配對不跨目錄；副檔名演進多候選與 A 類 R38 碰撞轉人工確認。
- A／B／C 產出保留相對路徑，C 類來源複本改用 R38 檔名但不轉換內容。
- 每次使用不存在的新輸出目錄及工作目錄租約；三份輸入保持不變。
- 錯誤或人工確認只建立 `Incomplete`，只有完整結果由核心建立 `Completed`。
- `aras-compare-core-tree` 引用正式受測核心，不合併或修改 R38 Core Tree。

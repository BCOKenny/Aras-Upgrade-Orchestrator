# 錯誤與停止條件

| 代碼或結果 | 觸發條件 | 處置 |
|---|---|---|
| `InvalidRequest` | 路徑、目標列舉或 evidence 缺失、不安全或不符本能力邊界。 | `Blocked`；不擴大搜尋、不比較內容。 |
| `None` | exact path 不存在，且同目錄同主檔名的允許演進候選為零。 | 正常回傳；交由分類能力決定後續。 |
| `MultipleTargetMappings` | 合法候選超過一個。 | `Blocked`；回傳 `Ambiguous` 和全部候選，等待人工確認。 |

不得把跨目錄候選、內容相似或使用者要求「選最可能」當成解除 `MultipleTargetMappings` 的依據。此 Skill 也不得產生 A／B／C、修改來源或建立交付目錄。

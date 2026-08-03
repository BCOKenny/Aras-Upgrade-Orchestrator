# AML 結構、節點語意與遞迴處理標準

> 建議檔案位置：`docs/standards/AML_Structure_and_Traversal_Standard.md`  
> 文件性質：跨功能共用的技術標準（Shared Technical Standard）  
> 適用範圍：Aras Innovator AML 解析、Package 比對、Core Tree 比對、AML 複製、AML 修改、差異報表與升級工具

---

## 1. 文件目的

本文件定義專案內所有 AML 相關功能必須共同遵守的：

1. AML 階層結構。
2. Item、Property、Relationships 與 Relationship Item 的語意。
3. Item 型別屬性的辨識方式。
4. AML 遞迴展開及遍歷規則。
5. AML 節點路徑表示方式。
6. AML 比對、複製與修改的最低要求。
7. Work、Codex 與開發人員執行時的引用規則。

本文件不是單一功能的 WorkSpec 或 FunctionSpec，而是多個功能共用的基礎技術規範。

任何 WorkSpec、FunctionSpec 或實作 Prompt，只要涉及 AML，都必須明確引用本文件。

---

## 2. 建議放置目錄

### 2.1 建議位置

```text
<ProjectRoot>/
├─ AGENTS.md
├─ README.md
├─ docs/
│  ├─ standards/
│  │  └─ AML_Structure_and_Traversal_Standard.md
│  ├─ work-specs/
│  ├─ function-specs/
│  └─ design/
├─ src/
└─ tests/
```

建議正式位置：

```text
docs/standards/AML_Structure_and_Traversal_Standard.md
```

### 2.2 不建議放在 `skills/`

本文件不建議直接放在：

```text
skills/
```

原因如下：

- `skills/` 通常代表可重複執行的 Agent 工作流程、操作能力或工具使用方法。
- 本文件主要定義 AML 的資料模型、節點語意與程式實作約束。
- 它屬於專案的共用技術標準，而不是一個獨立可執行的 Skill。
- 將標準文件放入 `skills/`，容易讓 Work 或 Codex 誤認為只有執行特定 Skill 時才需要閱讀。

只有當未來另外建立「AML 分析 Skill」時，才建議建立：

```text
skills/
└─ ArasAmlAnalysis/
   └─ SKILL.md
```

而該 Skill 應再引用本文件：

```text
docs/standards/AML_Structure_and_Traversal_Standard.md
```

---

## 3. 文件引用優先順序

只把文件放入專案目錄，不能保證 Work 或 Codex 一定會主動讀取。

應建立以下引用鏈：

```text
AGENTS.md
   ↓
WorkSpec / FunctionSpec
   ↓
AML_Structure_and_Traversal_Standard.md
   ↓
程式實作與測試
```

建議優先順序：

1. `AGENTS.md`：宣告所有 AML 工作都必須閱讀本文件。
2. `WorkSpec`：說明本次工作適用此標準。
3. `FunctionSpec`：將相關章節列為實作約束。
4. 執行 Prompt：再次要求先讀取指定文件。
5. 程式碼及測試：使用本文件定義的節點分類與驗收案例。

---

## 4. `AGENTS.md` 建議加入內容

將以下內容加入專案根目錄的 `AGENTS.md`：

```markdown
## AML 開發共用標準

凡涉及 Aras AML 的解析、比對、複製、修改、輸出、Package 升級或 Core Tree 升級功能，執行前必須先閱讀：

- `docs/standards/AML_Structure_and_Traversal_Standard.md`

AML 不可只視為一般 XML。

實作必須明確區分：

- AML Root
- Item
- Scalar Property
- Item Property
- Relationships Container
- Relationship Item

禁止只處理第一層 Item 或第一層 Relationships。

若本次 WorkSpec、FunctionSpec 或既有程式與上述標準衝突，應先列出衝突，不可直接自行選擇其中一種規則。
```

---

## 5. WorkSpec 建議引用方式

涉及 AML 的 WorkSpec 應加入：

```markdown
## 共用技術標準

本工作涉及 Aras AML 階層解析與節點比對。

執行本 WorkSpec 前，必須閱讀並遵守：

- `docs/standards/AML_Structure_and_Traversal_Standard.md`

本 WorkSpec 僅定義本功能的業務目標、輸入、輸出及範圍。
AML 節點分類、遞迴處理及路徑表示方式，以共用標準文件為準。

若本 WorkSpec 與共用標準衝突，必須先停止實作並列出衝突項目。
```

---

## 6. FunctionSpec 建議引用方式

涉及 AML 的 FunctionSpec 應加入：

```markdown
## AML 實作依據

本功能的 AML 解析、比對及複製行為，必須遵守：

- `docs/standards/AML_Structure_and_Traversal_Standard.md`

以下內容不得在本 FunctionSpec 中另行簡化：

1. Item 型別屬性的辨識方式。
2. Relationships 與 Relationship Item 的區分。
3. 巢狀 Item 的無固定深度遞迴。
4. 完整子樹複製。
5. AML Path 的輸出方式。
6. 未能建立可靠比對 Key 時轉入人工確認。
```

---

## 7. AML 基本階層模型

AML 的基本結構如下：

```text
AML
└─ Item
   ├─ Scalar Property
   ├─ Item Property
   │  └─ Item
   │     ├─ Scalar Property
   │     ├─ Item Property
   │     │  └─ Item
   │     └─ Relationships
   │        └─ Relationship Item
   └─ Relationships
      └─ Relationship Item
         ├─ Scalar Property
         ├─ Item Property
         │  └─ Item
         └─ Relationships
            └─ Relationship Item
```

此結構可以持續向下遞迴，不得假設固定深度。

---

## 8. AML 範例

```xml
<AML>
  <Item
    type="ItemType"
    id="B88C14B99EF449828C5D926E39EE8B89"
    action="add">

    <name>Document</name>
    <label>Document</label>

    <default_form keyed_name="Document" type="Form">
      <Item
        type="Form"
        id="EB81131688454BD0B885176C178C443B"
        action="get">

        <name>Document</name>

        <Relationships>
          <Item type="Body" action="get">
            <sort_order>128</sort_order>
          </Item>
        </Relationships>
      </Item>
    </default_form>

    <Relationships>
      <Item type="Property" action="add">
        <name>created_by_id</name>
        <data_type>item</data_type>

        <data_source keyed_name="User" type="ItemType">
          <Item
            type="ItemType"
            id="45E899CD2859442982D033417B8CECB4"
            action="get">
            <name>User</name>
          </Item>
        </data_source>
      </Item>

      <Item type="View" action="add">
        <type>default</type>

        <related_id keyed_name="Document" type="Form">
          <Item
            type="Form"
            id="EB81131688454BD0B885176C178C443B"
            action="get">
            <name>Document</name>
          </Item>
        </related_id>
      </Item>
    </Relationships>
  </Item>
</AML>
```

---

## 9. 節點類型定義

### 9.1 AML Root

```xml
<AML>
```

`AML` 是一個或多個頂層 Item 的容器。

程式不可假設一份 AML 只有一個頂層 Item。

---

### 9.2 Item

符合以下格式的節點視為 Item：

```xml
<Item type="..." id="..." action="...">
```

Item 常見識別資訊包括：

| 欄位 | 說明 |
|---|---|
| `type` | Aras ItemType 名稱 |
| `id` | Item ID，可能不存在 |
| `action` | add、edit、get、delete、merge 等 AML 動作 |
| `where` | AML 查詢條件，可能取代 id |
| `select` | 查詢欄位 |
| `related_expand` | Relationship 展開設定 |

Item 可能出現在：

1. AML 的直接子階。
2. Item 型別屬性的子階。
3. Relationships 的直接子階。
4. Relationship Item 的 Item 型別屬性子階。
5. 巢狀 Item 的 Relationships 子階。

---

### 9.3 Scalar Property

Item 下不包含直接子階 Item 的一般屬性，視為 Scalar Property。

例如：

```xml
<name>Document</name>
<label>Document</label>
<sort_order>128</sort_order>
```

其邏輯結構：

```text
Item
└─ Scalar Property
   └─ Value
```

程式應保留：

- Property 名稱。
- 文字值。
- XML Attributes。
- `xml:lang`。
- `condition`。
- 空值狀態。
- CDATA。
- Namespace。

範例：

```xml
<label xml:lang="en">Document</label>
<created_on condition="ge">2026-01-01</created_on>
```

---

### 9.4 Item Property

當 Item 的屬性節點包含直接子階 Item 時，該屬性視為 Item Property。

例如：

```xml
<related_id keyed_name="Document" type="Form">
  <Item type="Form" id="..." action="get">
    <name>Document</name>
  </Item>
</related_id>
```

邏輯結構：

```text
Parent Item
└─ Item Property：related_id
   └─ Referenced Item：Form
```

`related_id` 本身不是 Relationship Item。

常見 Item Property 名稱包括：

```text
source_id
related_id
owned_by_id
managed_by_id
permission_id
data_source
default_form
role
execution_allowed_to
```

但程式不可只依節點名稱判斷。

主要判斷方式應為：

> Item 的一般子節點中，是否包含直接子階 `<Item>`。

---

### 9.5 Relationships Container

```xml
<Relationships>
```

`Relationships` 是 Relationship Item 的集合容器。

它：

- 不是 Scalar Property。
- 不是 Item Property。
- 不是 Relationship Item。
- 不應當作一般 XML 屬性值處理。

範例：

```xml
<Relationships>
  <Item type="Property" action="add">
  </Item>

  <Item type="View" action="add">
  </Item>
</Relationships>
```

---

### 9.6 Relationship Item

位於 `Relationships` 直接子階的 Item，視為 Relationship Item。

例如：

```xml
<Relationships>
  <Item type="Property" action="add">
    <name>item_number</name>
  </Item>
</Relationships>
```

其中：

```xml
<Item type="Property">
```

是 Relationship Item。

Relationship Item 本身仍是完整 Item，因此可以包含：

- Scalar Property。
- Item Property。
- `related_id`。
- `source_id`。
- 下一層 Relationships。
- 下一層 Relationship Item。

---

## 10. 節點分類優先順序

程式辨識節點時，必須依以下順序判斷：

```text
1. 節點名稱是否為 AML
2. 節點名稱是否為 Item
3. 節點名稱是否為 Relationships
4. 節點是否包含直接子階 Item
5. 其他情況視為 Scalar Property
```

不可只根據 `source_id`、`related_id` 或 `data_source` 等名稱直接判斷節點類型。

---

## 11. 遞迴處理規則

對任一 Item，必須執行：

```text
ProcessItem(CurrentItem)
│
├─ 讀取 Item Attributes
│  ├─ type
│  ├─ id
│  ├─ action
│  ├─ where
│  └─ 其他 Attributes
│
├─ 逐一處理直接子節點
│
├─ 若子節點名稱為 Relationships
│  └─ 對每一個直接子 Item 呼叫 ProcessRelationshipItem
│
├─ 若子節點包含直接子 Item
│  └─ 分類為 Item Property
│     └─ 對其子 Item 再呼叫 ProcessItem
│
└─ 其他節點
   └─ 分類為 Scalar Property
```

Relationship Item 的處理：

```text
ProcessRelationshipItem(RelationshipItem)
│
├─ 記錄 Relationship Item 的 type、id、action
├─ 處理 Scalar Property
├─ 處理 Item Property
├─ 處理 related_id 或 source_id 下的 Item
└─ 若存在 Relationships，繼續向下遞迴
```

不得設定固定遞迴深度。

如需防止異常輸入造成無限處理，應使用：

- XML 節點實體追蹤。
- 循環參考防護。
- 可設定但不影響正常 AML 的安全上限。

不可因方便而只處理固定二層或三層。

---

## 12. AML Path 表示方式

規格、Log、錯誤訊息與差異報表應使用 AML Path 表示節點位置。

### 12.1 Item

```text
/AML/Item[type=ItemType, id=B88C14B99EF449828C5D926E39EE8B89]
```

### 12.2 Scalar Property

```text
/AML/Item[type=ItemType]/ScalarProperty[name=label]
```

### 12.3 Item Property

```text
/AML/Item[type=View]/ItemProperty[name=related_id]/Item[type=Form]
```

### 12.4 Relationship Item

```text
/AML/Item[type=ItemType]/Relationships/Item[type=Property]
```

### 12.5 Relationship Item 下的 Item Property

```text
/AML/Item[type=ItemType]
/Relationships
/Item[type=Property, name=created_by_id]
/ItemProperty[name=data_source]
/Item[type=ItemType, name=User]
```

---

## 13. 建議節點模型

實作前應建立統一 AML 節點模型。

### 13.1 分類模型

```text
AmlNode
├─ AmlRootNode
├─ AmlItemNode
├─ AmlScalarPropertyNode
├─ AmlItemPropertyNode
├─ AmlRelationshipsNode
└─ AmlRelationshipItemNode
```

### 13.2 單一遞迴模型

也可使用單一模型：

```text
AmlNode
- NodeKind
- Name
- Value
- Attributes
- Parent
- Children
- Path
- Depth
- ItemType
- ItemId
- Action
```

`NodeKind` 至少必須支援：

```text
AmlRoot
Item
ScalarProperty
ItemProperty
RelationshipsContainer
RelationshipItem
```

---

## 14. 比對規則

AML 比對單位：

```text
AML
└─ Item Compare Unit
   ├─ Item Attributes
   ├─ Scalar Property Compare Unit
   ├─ Item Property Compare Unit
   │  └─ Nested Item Compare Unit
   └─ Relationship Collection
      └─ Relationship Item Compare Unit
```

### 14.1 一般 Item Key

左右資料配對可使用：

```text
Normalized(type) + Normalized(id)
```

同側重複資料判定可使用：

```text
Normalized(type) + Normalized(id) + Normalized(action)
```

實際規則仍應由個別 WorkSpec 或 FunctionSpec 定義。

### 14.2 Relationship Item Key

Relationship Item 不可只依 XML 順序配對。

可依 Relationship Type 的實際識別方式使用：

```text
type + id
```

或：

```text
type + name
```

或：

```text
type + related_id
```

或：

```text
type + composite key
```

無法建立可靠 Key 時：

```text
Manual Review
```

不可直接依索引或出現順序自動配對。

---

## 15. 複製與修改規則

複製 Item Property 或 Relationship Item 時，必須複製完整 XML 子樹。

例如：

```xml
<related_id>
  <Item type="Form">
    <name>Document</name>
  </Item>
</related_id>
```

必須完整保留：

- Property 節點。
- Property XML Attributes。
- 子階 Item。
- Item Attributes。
- Item 的所有 Property。
- Item 的 Relationships。
- 更深層所有 Item。
- Namespace 與語系資訊。

不可只複製：

- Item ID。
- Property 文字值。
- 第一層元素。
- 固定深度內容。

---

## 16. 禁止的實作方式

以下做法不符合本標準：

1. 只解析 Item 的第一層 Property。
2. 只處理第一層 Relationships。
3. 將 `related_id` 下的 Item 視為 Relationship Item。
4. 將 Relationships 視為一般 Property。
5. 假設所有 Property 都是純文字。
6. 假設 Item Property 最多只有一層 Item。
7. 使用 XML 順序作為 Relationship Item 唯一配對依據。
8. 複製 Item Property 時只複製 ID。
9. 遇到未知巢狀階層時直接忽略。
10. 對 AML 階層設定固定深度限制。
11. 直接以一般 XML 節點名稱取代 AML 語意模型。
12. 未輸出 AML Path，導致錯誤無法追蹤到實際節點。

---

## 17. 最低驗收案例

### 17.1 Scalar Property

```xml
<Item type="Part">
  <item_number>P-001</item_number>
</Item>
```

預期：

```text
Part
└─ Scalar Property：item_number
```

### 17.2 Item Property

```xml
<Item type="View">
  <related_id>
    <Item type="Form">
      <name>Document</name>
    </Item>
  </related_id>
</Item>
```

預期：

```text
View
└─ Item Property：related_id
   └─ Form Item
```

### 17.3 Relationship Item

```xml
<Item type="ItemType">
  <Relationships>
    <Item type="Property">
      <name>item_number</name>
    </Item>
  </Relationships>
</Item>
```

預期：

```text
ItemType
└─ Relationships
   └─ Relationship Item：Property
```

### 17.4 Relationship Item 下的 Item Property

```xml
<Item type="ItemType">
  <Relationships>
    <Item type="Property">
      <data_source>
        <Item type="ItemType">
          <name>List</name>
        </Item>
      </data_source>
    </Item>
  </Relationships>
</Item>
```

預期完整辨識：

```text
ItemType
└─ Relationships
   └─ Relationship Item：Property
      └─ Item Property：data_source
         └─ ItemType Item：List
```

### 17.5 多層 Relationships

巢狀 Item 中再次出現 Relationships 時，程式必須繼續遞迴，直到沒有下一層 Item 或 Relationships。

### 17.6 完整子樹複製

複製 Item Property 時，輸出 XML 必須與來源子樹在語意上等價，且不得遺漏：

- Attributes。
- Namespace。
- Scalar Property。
- Item Property。
- Relationships。
- Nested Item。

---

## 18. Work 執行 Prompt 範本

將本文件放入專案後，執行 Work 時可使用：

```text
請先導讀本專案，並依序閱讀：

1. AGENTS.md
2. 本次 WorkSpec
3. 本次 FunctionSpec
4. docs/standards/AML_Structure_and_Traversal_Standard.md

本次工作涉及 Aras AML。

請先輸出：
- 已閱讀文件清單
- 本次功能涉及的 AML 節點類型
- AML 遞迴範圍
- 比對 Key
- 可能需要人工確認的情況
- 現有程式與 AML 共用標準的衝突

在完成上述分析前，不要修改程式。

實作時必須遵守 AML 共用標準，不可只處理第一層 Item、第一層 Property 或第一層 Relationships。
```

---

## 19. Codex 執行 Prompt 範本

```text
執行本功能前，先閱讀：

- AGENTS.md
- <本次 WorkSpec 路徑>
- <本次 FunctionSpec 路徑>
- docs/standards/AML_Structure_and_Traversal_Standard.md

先檢查現有 AML parser、compare、copy、modify 程式是否符合共用標準。

請先列出：
1. 現有 AML 節點模型。
2. Item Property 的辨識方式。
3. Relationships 與 Relationship Item 的辨識方式。
4. 遞迴是否有固定深度。
5. 完整子樹是否會被保留。
6. 差異報表是否能輸出 AML Path。
7. 不符合標準的程式位置。

完成分析後，再依 FunctionSpec 實作。

不得因既有程式只支援一層結構，而降低或忽略 AML 共用標準。
```

---

## 20. 是否只放到目錄就會自動被讀取

答案：不能保證。

Work 或 Codex 是否讀取文件，取決於：

- 專案是否有 `AGENTS.md`。
- `AGENTS.md` 是否列出必讀文件。
- 本次 Prompt 是否指定文件。
- WorkSpec 或 FunctionSpec 是否有明確引用。
- 執行範圍是否包含該目錄。
- Agent 是否先進行專案導讀。

因此建議至少完成三層引用：

```text
AGENTS.md
+ WorkSpec / FunctionSpec
+ 執行 Prompt
```

不要只依賴檔案位於相對應目錄。

---

## 21. 最佳實務

### 必須做到

- 將本文件放在 `docs/standards/`。
- 在 `AGENTS.md` 宣告 AML 工作必讀。
- 在每個 AML 相關 WorkSpec 與 FunctionSpec 引用。
- 每次正式執行 Prompt 再指定一次文件路徑。
- 要求 Work 或 Codex 先列出已閱讀文件。
- 先做規格衝突分析，再修改程式。

### 建議做到

- AML 共用標準只維護一份。
- WorkSpec 不重複貼整份 AML 定義。
- FunctionSpec 只補充該功能特有的比對 Key、忽略欄位與輸出規則。
- 修改本標準後，檢查所有 AML 相關 FunctionSpec 是否受影響。
- 測試案例檔可放在：

```text
tests/fixtures/aml/
```

例如：

```text
tests/fixtures/aml/
├─ scalar_property.xml
├─ item_property.xml
├─ relationship_item.xml
├─ nested_item_property.xml
└─ nested_relationships.xml
```

---

## 22. 文件維護規則

若未來修改 AML 共用標準，應同步記錄：

```text
- 變更日期
- 變更原因
- 受影響功能
- 受影響 WorkSpec
- 受影響 FunctionSpec
- 是否需要修改既有測試
- 是否需要重新產生差異報表
```

建議在文件底部維護版本紀錄。

---

## 23. 版本紀錄

| 版本 | 日期 | 說明 |
|---|---|---|
| 1.0 | 2026-07-29 | 建立 AML 階層、節點語意、遞迴處理、引用方式與 Work/Codex 執行規則 |

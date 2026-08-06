## Agent skills

### Project-local editing policy

在本專案建立、修改、搬移或刪除任何檔案前，必須先讀取並遵守：

- `.agents/skills/edit-project-directly/SKILL.md`

此 Skill 是本機編輯限制與文件語言政策的 single source of truth；其規則優先於其他 Skill 中衝突的工作流程步驟。

### Issue tracker

Issues are tracked as local Markdown files under `.scratch/`. See `docs/agents/issue-tracker.md`.

### Domain docs

This repository uses a single-context domain-documentation layout. See `docs/agents/domain.md`.

### AML shared standard

Before any work involving Aras AML parsing, comparison, copying, modification, output, Package upgrade, or related tests, read:

- `docs/standards/AML_Structure_and_Traversal_Standard.md`

AML must not be treated as generic XML. Distinguish AML Root, Item, Scalar Property, Item Property, Relationships Container, and Relationship Item; recurse without assuming a fixed depth. If another requirement or existing implementation conflicts with this standard, report the conflict before proceeding.

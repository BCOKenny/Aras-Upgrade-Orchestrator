## Agent skills

### Issue tracker

Issues are tracked as local Markdown files under `.scratch/`. See `docs/agents/issue-tracker.md`.

### Domain docs

This repository uses a single-context domain-documentation layout. See `docs/agents/domain.md`.

### AML shared standard

Before any work involving Aras AML parsing, comparison, copying, modification, output, Package upgrade, or related tests, read:

- `docs/standards/AML_Structure_and_Traversal_Standard.md`

AML must not be treated as generic XML. Distinguish AML Root, Item, Scalar Property, Item Property, Relationships Container, and Relationship Item; recurse without assuming a fixed depth. If another requirement or existing implementation conflicts with this standard, report the conflict before proceeding.

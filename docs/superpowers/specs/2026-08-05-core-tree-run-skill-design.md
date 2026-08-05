# Core Tree execution Skill design

## Decision

Create `aras-run-core-tree-comparison` as a separate functional Skill. It has a different trigger and safety boundary from `aras-compare-core-tree`: the former runs a case-bound attempt; the latter validates inputs, compares content, classifies A/B/C, and builds output.

## Boundary

The Skill coordinates the eight fixed steps: case/settings load, identity/version validation, immutable snapshot, new attempt, safety policy and directory lease, formal command/action gate, append-only history, and fixed result. It must stop with `Blocked` while `CoreTreeComparisonCommand`/CLI/UI is not implemented. It must never generate the missing command at runtime or access customer, DB, Aras, or K: environments.

## Contract

The formal command is the only entry point allowed to bind a case snapshot to `CoreTreeComparisonBuilder`. Core-only tests remain offline fixtures and are not customer-case evidence. A completed result requires the existing Core Tree completion rules and evidence; manual review produces `Incomplete` or `Blocked`.

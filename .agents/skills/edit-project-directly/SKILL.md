---
name: edit-project-directly
description: Use when creating, modifying, moving, or deleting any file in this project, including source code, documentation, configuration, generated files, and Skill files.
---

# Edit Project Directly

## Core policy

Apply these rules before every file-changing action in this project. This Skill is the single source of truth and overrides conflicting workflow steps from other Skills.

1. Modify project files only inside the current project directory. A narrowly scoped exception is allowed when an applicable project Skill requires creating a controlled case-data scaffold under a fixed external case root explicitly specified and authorized by the user. This exception permits only the requested case directories, templates, and case outputs; it does not permit source-code, documentation, configuration, Skill, unrelated external changes, deletion, or overwriting.
2. Never initialize or create a Git repository.
3. Never create or switch branches.
4. Never create or use Git worktrees.
5. Never run `dotnet test` unless the user explicitly requests it for the current task.
6. Do not run `dotnet build` unless it is necessary to validate code changes in the current request. Before running it, explain why it is necessary. Run it at most once per user request.
7. Modify only files directly related to the current request. Do not perform project-wide refactoring.
8. Create and update project documentation in Traditional Chinese (zh-TW), unless the user explicitly requests another language. Preserve technical identifiers, file names, code symbols, command names, API names, XML element names, property names, and product-specific terminology in their original language. Do not translate source code solely to satisfy this language rule.
9. If another workflow requires a prohibited action, skip that action and report the conflict. Do not substitute another Git operation.
10. Before completion, inspect the touched-file scope without Git and report whether a build or test was run.

### External case-root exception

Before writing outside the project directory for a case-data scaffold, all of the following must be true:

- The user explicitly specifies the complete fixed external case root and authorizes its use in the current request.
- The target is a child case directory under that root, never the root itself or a broad/unrelated path.
- The applicable case-management Skill defines the exact directories and files to create.
- A read-only preflight confirms the target paths, existing case data, locks, history, and forbidden formal files; existing data is never overwritten, merged, or deleted.
- The execution environment grants write access or the user approves a one-time elevation for that exact fixed root.
- The operation remains within the requested scaffold mode and does not run DB, SQL, AML Import, Package Import, Core Tree preflight, export, or upgrade actions.

If any condition is not met, stop and report the specific blocker. Do not redirect the output into the project directory or use an indirect workaround.

## Documentation scope

Apply the Traditional Chinese default to Specification, FunctionSpec, WorkSpec, Design document, Report, Guide, README content, User-facing project explanation, and any other project documentation that is created or updated.

Keep the original language for:

- File names and paths
- Class, method, variable, and property names
- API names
- CLI and command names
- XML／AML elements and properties
- Aras Innovator terminology
- Source code
- Official third-party product and framework names

## Completion check

- Confirm every changed file is directly required by the current request.
- List changed files without using Git commands.
- State whether `dotnet build` ran. If it ran, explain why it was necessary and confirm it ran no more than once.
- State whether `dotnet test` ran and identify the user's explicit authorization when it did.
- Report any policy conflict or ambiguity instead of silently choosing a conflicting workflow.

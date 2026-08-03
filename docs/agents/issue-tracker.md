# Issue tracker: Local Markdown

Issues and specs (also known as PRDs) for this repository live as Markdown files in `.scratch/`.

## Conventions

- One feature per directory: `.scratch/<feature-slug>/`
- The spec is `.scratch/<feature-slug>/spec.md`
- Implementation issues are one file per ticket at `.scratch/<feature-slug>/issues/<NN>-<slug>.md`, numbered from `01`; never use a single combined tickets file.
- Comments and conversation history are appended to the bottom of the file under a `## Comments` heading.

## When a skill says "publish to the issue tracker"

Create a new file under `.scratch/<feature-slug>/`, creating the directory if needed.

## When a skill says "fetch the relevant ticket"

Read the file at the referenced path. The user will normally provide the path or issue number directly.

## Wayfinding operations

Used by `/wayfinder`. The map is a file with one child file per ticket.

- **Map:** `.scratch/<effort>/map.md` — the Notes, Decisions-so-far, and Fog body.
- **Child ticket:** `.scratch/<effort>/issues/NN-<slug>.md`, numbered from `01`, with the question in the body. A `Type:` line records the ticket type (`research`, `prototype`, `grilling`, or `task`); a `Status:` line records `claimed` or `resolved`.
- **Blocking:** A `Blocked by: NN, NN` line near the top. A ticket is unblocked when every file it lists is `resolved`.
- **Frontier:** Scan `.scratch/<effort>/issues/` for files that are open, unblocked, and unclaimed; the lowest number wins.
- **Claim:** Set `Status: claimed` and save before beginning work.
- **Resolve:** Append the answer under an `## Answer` heading, set `Status: resolved`, then append a context pointer (summary and link) to the map's Decisions-so-far section in `map.md`.

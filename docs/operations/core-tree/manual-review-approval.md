# Core Tree Manual Review Approval

Use this controlled operation only after every row in an attempt's `manual-review-register.md` is marked `Resolved` by the responsible owner.

The formal action is `CoreTreeManualReviewApprovalCommand`, exposed by the offline CLI:

```powershell
dotnet tools/ArasUpgradeOrchestrator.CoreTree.Cli/bin/Release/net8.0/ArasUpgradeOrchestrator.CoreTree.Cli.dll --approve-reviews <approval-request.json>
```

Start from [manual-review-approval-request.template.json](templates/manual-review-approval-request.template.json). Set `approvalOutputRoot` to a new directory under `<case-root>/core-tree/review-approvals/`; it must not already exist.

The comparison builder creates the register beside `manual-reviews.json` whenever an attempt is `Incomplete` with manual reviews. The command accepts only that generated register. It validates the original `incomplete-manifest.json`, formal `manual-reviews.json`, and each register row's ID, relative path, review code, decision, approver, ISO 8601 approval timestamp, and `Resolved` state. It checks the configured safety whitelist, takes a directory lease, writes a new `manual-review-approval.json`, and appends `core-tree.manual-reviews.approved` to case history.

It never edits inputs, R38, an old attempt directory, `manual-reviews.json`, `incomplete-manifest.json`, or history. Approval is deliberately **not** `Completed`: a later controlled delivery/application action must prove how every approved decision is applied and that the resulting output is compatible before it can create a completion manifest.

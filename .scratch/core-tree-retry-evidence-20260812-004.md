# Core Tree retry evidence

- Previous comparison attempt: `17763141-1a98-433e-b399-a86aabd3f12e` (`attempt-20260811-003`)
- Previous comparison completion receipt: `core-tree/completions/completion-20260812-001/completion-manifest.json`
- Reason: A new comparison attempt is required to produce the immutable `classification-result.json` and input-tree digest artifacts required by the formal `--build-delivery` capability. The prior attempt predates those artifacts and must not be altered.
- Retry basis: `VerifiedIdempotency`. The three isolated input trees remain read-only and the new attempt uses a unique output directory.
- Requested by: BCO\\kenny
- Recorded at: 2026-08-12T11:00:00+08:00

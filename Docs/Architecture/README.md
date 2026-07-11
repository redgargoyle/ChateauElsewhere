# Chateau architecture recovery index

1. [`FOUNDATION_PATCH_REPORT.md`](FOUNDATION_PATCH_REPORT.md) — exact changes, metrics, limitations and required Unity validation.
2. [`MIGRATION_REPORT.md`](MIGRATION_REPORT.md) — current implementation status, metrics, open gates and compatibility debt.
3. [`ARCHITECTURE.md`](ARCHITECTURE.md) — target mental model, hierarchy, ownership and chapter runtime flow.
4. [`MIGRATION_PLAN.md`](MIGRATION_PLAN.md) — gated path from current code to the target.
5. [`TESTING_AND_RECOVERY.md`](TESTING_AND_RECOVERY.md) — exact compile, install, test, smoke-test and rollback process.
6. [`PRUNE_LOG.md`](PRUNE_LOG.md) — completed deletions and candidates that are not yet safe.
7. [`HAMZA_CARDS.md`](HAMZA_CARDS.md) — concise learning and code-review cards.
8. [`CODEX_APPLY_AND_CONTINUE.md`](CODEX_APPLY_AND_CONTINUE.md) — execution prompt for applying and validating this patch.
9. [`RuntimeClassMigrationLedger.html`](RuntimeClassMigrationLedger.html) — searchable mapping of all baseline runtime source files.
10. [`TargetArchitecture.svg`](TargetArchitecture.svg) and [`MigrationRoadmap.svg`](MigrationRoadmap.svg) — visual maps.
11. `Baseline/` — machine-generated source and serialization evidence.
12. `Generated/` — regenerated reports from `Tools/architecture`.

The architecture guard permits existing legacy debt but prevents new files or edits from increasing it. Decrease the baseline only after a migration is complete; never reset it upward to make CI green.

Automation wrappers: `Tools/architecture/run_foundation_gate.ps1` and `Tools/architecture/run_foundation_gate.sh`.

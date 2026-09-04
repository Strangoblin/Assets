# 06_architecture — Phase 1/2 contract baseline

This test is intentionally read-only against legacy project architecture. It validates the Phase 2 shared-core scaffold and generates
ignored reports under `output/` and verifies that the known pre-migration metrics
remain explainable until a migration phase updates the fixture.

Use `python3 verify.py --strict` only after the corresponding migration phase is
complete; the baseline mode is expected to pass before migration.

# 06_architecture — Phase 1 contract baseline

This test is intentionally read-only against the project architecture. It generates
ignored reports under `output/` and verifies that the known pre-migration metrics
remain explainable until a migration phase updates the fixture.

Use `python3 verify.py --strict` only after the corresponding migration phase is
complete; the baseline mode is expected to pass before migration.

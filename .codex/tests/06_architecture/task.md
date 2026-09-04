# 06_architecture — Phase 4 migration contract baseline

This test is intentionally read-only against the platform adapters. It validates the Phase 2 shared-core scaffold, the Phase 3 shared-content migration and the Phase 4 MCP knowledge-root cutover, and generates
ignored reports under `output/` and verifies that the Phase 4 metrics remain explainable while later Claude/Codex adapter cutovers are pending.

Use `python3 verify.py --strict` only after the corresponding migration phase is
complete; the default mode is expected to pass at the end of Phase 4.

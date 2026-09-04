# Phase 3：迁移清单、契约测试与共享核心骨架

本测试不删除平台入口或兼容层；它扫描 `.agents/`、`.claude/`、`.codex/` 和 `.mcp/`，验证 Phase 2 共享核心骨架，并固化 Phase 3 迁移后的契约指标。

运行：

```bash
./run.sh
python3 verify.py --strict
```

默认模式使用 Phase 3 迁移 fixture 并生成报告；同时要求共享入口、角色骨架、知识 ID 契约和 project-root resolver 契约存在。迁移前指标仍保留在 `verify.py` 的 `PRE_MIGRATION_BASELINE`，只用于解释迁移差异：

- `output/migration-inventory.json`：每个 tracked 架构文件的当前路径、语义类别、拟议目标、引用者、迁移决策和理由。
- `output/baseline-findings.json`：断链、漂移、绝对路径、大小写错误和 MCP 硬编码明细。

`output/` 被 `.gitignore` 忽略。默认模式只有在已知基线指标发生未解释变化时失败；`--strict` 留给 Phase 4/5 切流完成后的验收阶段；Phase 3 允许并记录 6 个逐项裁决后的 skill 漂移文件，MCP 的旧 `.claude` 路径暂不在本阶段切换。

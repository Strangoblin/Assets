# Phase 1/2：迁移清单、契约测试与共享核心骨架

本测试不移动或删除旧权威内容；它扫描 `.agents/`、`.claude/`、`.codex/` 和 `.mcp/`，并验证 Phase 2 的共享核心骨架。

运行：

```bash
./run.sh
python3 verify.py --strict
```

默认模式固化迁移前基线并生成；同时要求 Phase 2 共享入口、角色骨架、知识 ID 契约和 project-root resolver 契约存在：

- `output/migration-inventory.json`：每个 tracked 架构文件的当前路径、语义类别、拟议目标、引用者、迁移决策和理由。
- `output/baseline-findings.json`：断链、漂移、绝对路径、大小写错误和 MCP 硬编码明细。

`output/` 被 `.gitignore` 忽略。默认模式只有在已知基线指标发生未解释变化时失败；`--strict` 留给后续迁移完成后的验收阶段；当前已知旧路径、漂移和断链仍会按基线报告。

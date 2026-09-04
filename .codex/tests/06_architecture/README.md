# Phase 1：迁移清单与契约测试

本测试不移动或删除文件，只对 `.agents/`、`.claude/`、`.codex/` 和 `.mcp/` 做只读扫描。

运行：

```bash
./run.sh
python3 verify.py --strict
```

默认模式固化迁移前基线并生成：

- `output/migration-inventory.json`：每个 tracked 架构文件的当前路径、语义类别、拟议目标、引用者、迁移决策和理由。
- `output/baseline-findings.json`：断链、漂移、绝对路径、大小写错误和 MCP 硬编码明细。

`output/` 被 `.gitignore` 忽略。默认模式只有在已知基线指标发生未解释变化时失败；`--strict` 留给迁移完成后的验收阶段。

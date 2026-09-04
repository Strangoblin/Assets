---
name: agent-architecture-cutover
description: 2026-09-04 agent 架构解耦收尾 — Codex 交接 P0-P3 执行：MCP live 切流验证、Claude 发现链验证、.codex 适配层切流、skills 软链方案裁决。
date: 2026-09-04
---

# Agent 架构解耦收尾（2026-09-04）

Codex 完成 Phase 2-4(共享层迁移 + MCP 切流)后交接 Claude 验证与收尾。本文记录 P0-P3 执行结论、稳定格式与遗留待办。

## 稳定 knowledge ID 格式(最终)

- `unity/<canonical-relative-path>` → 根 `.agents/agents/unity-developer/references/<canonical-path>`
- `rules/<canonical-relative-path>` → 根 `.agents/rules/<canonical-path>`
- basename 仅作兼容输入;**重复 basename 必须拒绝**(`AmbiguousKnowledgePath`),不再按扫描顺序取第一个
- 高优先级对:`unity/standard/shader/shader-structure.md`、`unity/standard/script/script-structure.md`
- 契约测试:`.mcp/tests/test_knowledge_paths.py`;解析实现:`.mcp/validation/knowledge_paths.py`

## MCP live 服务重载方式

- 启动:`.mcp.json` 注册 `uv run python server.py`,**每客户端会话独立进程** → 重启 = 重开会话/客户端
- 本会话实测:新进程已加载 `.agents` 解析;`gate_pass(g_knowledge)` 返回的 `resolved_details[].canonical_relative_path` 全部以 `.agents/agents/unity-developer/references/` 开头,无旧 `.claude` 路径
- Codex 侧持久化服务返回旧路径 = 进程未重载;修复方式 = 重启进程,不是 gate_reset

## Claude skills 兼容方案(裁决)

- 现状:`.claude/skills/` 保留 9 组**实体副本**(非软链),6 个文件与 `.agents` 漂移;`.agents` 侧为 Phase 3 裁决后较新内容;Claude CLI 不可用,无法外部探针,本会话即发现权威
- 方案一(直接发现 `.agents/skills/`)不成立:平台无自定义 skill 根机制
- 方案三(镜像)= 现状,漂移源头
- **裁决:方案二 — `.claude/skills/<name>` 逐个改为相对软链 → `.agents/skills/<name>`**,发现入口不变、正文单源、漂移归零
- ⛔ 执行需人工确认(删除 32 个 tracked 副本 + 平台发现边界变化);验证 = 重启会话 skill 仍可加载 + 正文来自 `.agents`;回滚 = `git revert`
- 附带收益:strict 验收剩余失败项(drift 6 + 副本内 1 处绝对路径)随切流清零

## Codex 适配层切流(Phase 6 完成)

- `.codex/AGENTS.md`、`INTERFACE.md`:薄适配入口,统一"根 AGENTS.md → `.agents/`";删除"直接读 .claude/"叙述
- `.codex/agents/*.toml` ×2:262/249 行内嵌正文副本 → ~40 行运行时薄适配(正文指向共享 AGENT.md),`.Codex` 大小写错误清零
- `auto-developer.md`/`exec-developer.md`:自举读取与命令路径切到 `.agents/`
- `verify.py --strict` 扫描边界:排除平台配置、`.codex/tmp/`、dated memory 快照(历史快照按规则不改),保留 `.claude/skills` 副本在扫描内直至切流
- 实测:`codex_case_hits` 12→0;absolute 130→1;strict 仅剩 skill 切流项;`codex exec` 只读探针能引用 `.agents/README.md` 与根 AGENTS.md 原文

## 旧兼容层保留范围与退役条件

| 层 | 保留范围 | 退役条件 |
|----|---------|---------|
| `.claude/agents/*.md` | 相对软链 → `.agents/agents/<role>/AGENT.md`(实测发现正常) | Claude/Codex/MCP/Unity 四链验证后,人工确认 |
| `.claude/rules/*.md` | 相对软链 → `.agents/rules/*.md`(实测注入正常) | 同上 |
| `.claude/skills/*` | 实体副本(漂移 6) | 人工确认软链切流(见上)后删除实体 |
| `.claude/agents/<role>/` 空目录壳 | memory/references/cli/scripts/templates 空壳 | 与软链退役同批 |

- `.claude/settings.local.json` 为机器本地(gitignored),verify 扫描已豁免
- 退役命令 `find -L .claude -type l -print` 应零输出;根 `AGENTS.md` 保持普通文件(非软链)

## 验证证据(2026-09-04)

```bash
bash .codex/tests/06_architecture/run.sh          # PASS(default fixture 不变)
python3 .codex/tests/06_architecture/verify.py --strict  # 仅剩 skills 切流 2 项
.mcp/.venv/bin/python .mcp/tests/test_knowledge_paths.py  # PASS
.mcp/.venv/bin/python .mcp/tests/test_recipes.py          # All passed
codex exec --sandbox read-only                        # 入口探针通过
unityctl status                                        # 见最终验收
```

提交:`docs(agent-architecture): record adapter cutover handoff closure`(或按实际序列)。

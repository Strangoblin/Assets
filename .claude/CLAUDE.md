# Unity Lab — 项目身份

> Unity 渲染实验与 Shader 研发项目。本文件是项目级 CLAUDE.md，全局路由文件会导航到此。

---

## 项目概述

Unity 6 URP 17+ 渲染技术实验室。研究方向：PCSS 软阴影、Boids 群集模拟、体积云渲染、全屏后处理特效、CelToon 卡通渲染。

## 技术栈

- **引擎**：Unity 6 (6000.x) + Universal Render Pipeline 17+
- **Shader**：HLSL / ShaderGraph / Compute Shader
- **自动化**：unityctl（Editor 远程控制）
- **脚本执行**：Roslyn（C# 运行时注入）
- **平台**：macOS / Metal

---

## 循环架构 (memory → agent → platform → skill → CLI → script → memory)

```
┌──────────────────────────────────────────────────────────┐
│  [1] Memory ←─────────────────────────────────────┐      │
│  │   .claude/agents/unity-developer/memory/        │      │
│  │   .claude/rules/                                │      │
│  │   会话启动加载 → 会话结束更新 (E1-E4)             │      │
│  ↓                                                  │      │
│  [2] Agent ───────────────────────────────────┐    │      │
│  │   .claude/agents/unity-developer.md         │    │      │
│  │   宪法 C1-C7 + 模式选择 + 退出条件           │    │      │
│  │   .claude/agents/meta-developer.md          │    │      │
│  │   体系维护 + 精简去重 (P1-P3)                │    │      │
│  ↓                                             │    │      │
│  [3] Platform ───────────────────────────┐    │    │      │
│  │   agent.md 内 Editor 可用性策略         │    │    │      │
│  │   Editor 状态 → 流水线深度              │    │    │      │
│  ↓                                        │    │    │      │
│  [4] Skill ────────────────────────┐     │    │    │      │
│  │   .claude/skills/auto-manager/   │     │    │    │      │
│  │   过程性知识 + 工作流编排          │     │    │    │      │
│  ↓                                   │     │    │    │      │
│  [5] CLI ────────────────────┐      │     │    │    │      │
│  │   unity-developer/cli/unityctl.md  │     │    │    │      │
│  │   unity-developer/cli/roslyn.md    │     │    │    │      │
│  ↓                             │      │     │    │    │      │
│  [6] Script ───────────┐      │      │     │    │    │      │
│  │   agents/unity-developer/scripts/roslyn/     │      │      │     │    │    │      │
│  │   scene-query.cs     │      │      │     │    │    │      │
│  │   scene-organize.cs  │      │      │     │    │    │      │
│  ↓                       │      │      │     │    │    │      │
│  [7] → Memory ───────────┘      │      │     │    │    │      │
│   memory/ 创建 dated 文件 + 更新索引         │    │    │      │
│   rules/ 追加新错误模式 (grep 去重)           │    │    │      │
└──────────────────────────────────────────────────────────┘
```

---

## 入口门禁 [G0]

> 任何文件写入操作前必须通过此门禁。

**OUTPUT 格式：**
```
## G0: Framework Check
Agent: unity-developer | meta-developer
Action: proceed | load agent first
```

- 涉及 `Assets/Mine/` 写入 → Agent 必须为 `unity-developer`，否则先加载。写入必须走 MCP `write_gated`（原生 Write/Edit 已被 settings deny）。
- 涉及 `.claude/` 写入 → Agent 必须为 `meta-developer`，否则先加载
- 纯咨询/只读 → 跳过 G0
- MCP 工具发现：`gate_list` 查看所有门禁和配方；用法速查 → [agents/unity-developer/references/mcp-gate-usage.md](agents/unity-developer/references/mcp-gate-usage.md)（后果验证 v2：链唯一 [g_entry, g_knowledge]，write_gated 内容规范检查，Codex 经 check_norm.py CLI 对等）
- 🟢 **门禁 = 知识证据 + 后果验证**：任何写入都过 [g_entry, g_knowledge] 走 write_gated——知识声明必须命中真实文件，内容必须符合结构规范（error 阻断 / warning 提示）。Codex 产出自查：`python .mcp/validation/check_norm.py <file>`。

---

## 宪法 (C1-C7)

宪法是项目的最高原则。唯一权威来源：[agents/unity-developer.md](agents/unity-developer.md) — C1-C7 + 模式选择 + 退出条件 + 完整性门禁。

---

## 快速参考

- **项目根目录**：`/Users/xiaokangji/Unity/Lab`
- **关键代码目录**：`Assets/Mine/Shaders/`、`Assets/Mine/Scripts/`
- **知识库**：`agents/unity-developer/references/`（2026-08-24 自 `Assets/MarkDowns/` 内化迁移，归属 unity-developer）
- **Editor 检查**：`unityctl status`
- **Memory**：[agents/unity-developer/memory/MEMORY.md](agents/unity-developer/memory/MEMORY.md)
- **开发规范**：[.claude/rules/](rules/)

## Agent 路由

| 任务类型 | Agent | 触发关键词 |
|---------|-------|-----------|
| Unity 开发（Shader、C#、渲染） | [unity-developer](agents/unity-developer.md) | Shader, HLSL, Compute, RenderGraph, URP, Material |
| .claude 体系维护 | [meta-developer](agents/meta-developer.md) | agent, skill, reference, .claude, 体系, 结构, 维护 |
| Codex 落地执行（已规划任务） | exec-developer（Codex 侧） | codex exec、派发、落地、编译修复、写代码 |
| Codex 自主全流程 | auto-developer（Codex 侧） | 自主、自动、全流程、auto |

全局路由表在 `~/.claude/agents/default.md`。

> **Codex 侧路由**：需要 Codex 执行时，按 [codex-orchestrate](skills/codex-orchestrate/SKILL.md) skill 派发并路由到 **exec-developer**（`.codex/agents/exec-developer/exec-developer.md`，落地执行边界，任务书驱动）；用户要求自主全流程时路由到 **auto-developer**（`.codex/agents/auto-developer/auto-developer.md`）。Codex 同样执行开发任务（非仅"基础工作"），知识直接读 `.claude/` 权威源（入口指引：`.codex/AGENTS.md`）；`Assets/Mine/` 产出经 Claude review + write_gated 门禁链合入，**Codex 合入前自查规范**：`python .mcp/validation/check_norm.py <file>`（与 write_gated 同一检查）。任务书字段：目标 / 涉及文件 / 约束 / 验收标准 / 模式。

> **架构说明**：`rules/` 和 `skills/` 虽全为 Unity 内容，但因 Claude Code 的路径限定加载（`paths:` frontmatter）和 Skill 发现机制要求它们必须在顶级 `.claude/` 下，无法移入 `agents/unity-developer/`。将来加入非 Unity agent 时，其 rules 和 skills 将共存于同名顶级目录。`references/` 则相反——已按归属归入各 agent（unity-developer 全部渲染知识、meta-developer 体系知识）。规则按域划分：Unity 开发规范（shader-development/compute-shader/csharp-renderpass，`paths:` 限定到 `Assets/Mine/`）与体系架构规则（[meta-architecture.md](rules/meta-architecture.md)，`paths:` 限定到 `.claude/**` + `.mcp/**`）共存于顶层 rules/。

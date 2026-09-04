# Unity Lab — 项目 Agent 入口

本文件是平台无关的仓库入口。共享 agent 体系的唯一权威源是 `.agents/`；`.claude/` 和 `.codex/` 只提供各自客户端的薄适配，`.mcp/` 提供执行门禁。

## 项目身份

Unity 6 + URP 17+ 渲染技术实验室，涉及 Shader / HLSL / Compute / RenderGraph / C# 与 Unity Editor 验证。

## 渐进读取顺序

1. 读取 `.agents/README.md`。
2. 根据任务选择 `.agents/agents/<role>/AGENT.md`：
   - `unity-developer`：Unity 功能、渲染、脚本和 Editor 验证。
   - `meta-developer`：agent 体系、skills、rules、路径和适配层维护。
3. 读取任务相关的 `.agents/rules/` 和 `.agents/knowledge/<domain>/` 索引。
4. 按索引读取具体 reference、template、CLI、script 或 memory；不要扫描整个共享目录。

## 平台适配

- Claude：`.claude/CLAUDE.md`、settings、hooks 与 Claude 发现兼容层。
- Codex：`.codex/AGENTS.md`、`INTERFACE.md`、config、hooks 与测试。
- MCP：`.mcp/` 的 gate、validation 和测试；迁移完成后读取 `.agents` 的规范路径。

## 安全与回退

- 删除、移动旧副本或架构清理前，必须列出精确清单并取得人工确认。
- 禁止 `git stash --all`。
- 不修改 `Assets/Mine/` 功能代码，除非任务明确要求并完成对应门禁。
- 文档和配置使用仓库相对路径；不写死用户机器绝对路径。
- 每个架构阶段独立提交并保留可回退点。

## 迁移状态

当前为 Agent 架构重构 Phase 6：共享 rules、角色定义、角色归属内容和 MCP knowledge roots 已切换到 `.agents`；Claude 适配层（agent/rules 软链）与 Codex 适配层（入口/运行时角色）已完成切流与验证。待办（需人工确认）：`.claude/skills/` 逐 skill 软链切流与 Phase 7 旧副本清理。

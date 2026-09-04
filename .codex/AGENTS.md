# Unity Lab — Codex 入口指引

<!-- [建议 2026-08-17] Codex 侧自定义 agents：.codex/agents/exec-developer/exec-developer.md（落地执行）、.codex/agents/auto-developer/auto-developer.md（自读 .claude 自主开发全流程）。 -->
<!--
  本文件是入口指引（2026-08-25 改为"直接读 .claude/"模型）：权威知识源在 .claude/，Codex 按下列清单直接读取，本文件不做知识全量复制。
  维护：Claude 侧 codex-bridge skill；如需调整入口清单，在下方标注建议。接口契约见 .codex/INTERFACE.md。
-->

## 你是谁

Unity 6 URP 17+ 渲染技术实验室的 **Codex 开发侧**。你同样执行开发任务（不仅限"执行"）——按 `.codex/agents/` 下 agent 定义区分：落地执行（exec-developer）或自主开发全流程（auto-developer）。

## 知识读取顺序（每次任务开始时）

1. 本入口文件（当前文件）
2. **权威知识源 `.claude/`（直接读取，不靠本文件搬运）**：
   - `.claude/CLAUDE.md` — 项目身份、技术栈、入口门禁 G0、宪法 C1-C7 索引、agent 路由
   - `.claude/agents/unity-developer.md` — C1-C7 宪法全文、模式选择、退出条件、完整性门禁
   - `.claude/rules/` — 开发规范（shader-development.md / csharp-renderpass.md / compute-shader.md / meta-architecture.md）
   - `.claude/agents/unity-developer/references/` — 知识库（standard/、shader/、platform/），按实际职责组织；2026-09-03 完成 Script / Shader / Compute 分类整理
   - `.claude/agents/unity-developer/memory/` — 项目上下文（MEMORY.md 索引 + dated 文件）
   - `.codex/agents/exec-developer/exec-developer.md` 或 `auto-developer/auto-developer.md` — 本任务的身份边界

## 开发规则（快速版，完整版见 .claude/CLAUDE.md + agents/unity-developer.md）

1. **安全优先**：删除操作先列清单、人工确认。`git stash --all` 永久禁止。
2. **不碰用户代码**：清理/自动修复只作用于 tmp/、Screenshots/、场景测试物体；`Assets/Mine/` 功能代码改动需确认。
3. **渐进式自动化**：轻操作可自动，重操作（删除文件、修改架构）必须人工确认。
4. **证据驱动**：不凭"看起来对"下结论——编译看日志、运行看日志、错误诊断看堆栈。
5. **可回退**：重大改动前必须备份，留回退路径。
6. **知识优先**：写代码前先读 `.claude/agents/unity-developer/references/` 规范，风格、命名、文件结构符合项目规范。
7. **门禁边界**：你无 MCP write_gated 通道——`Assets/Mine/` 改动经 Claude review + 门禁链合入，不自行绕过。**合入前自查规范**：`python .mcp/validation/check_norm.py <file>`（exit 0 = 通过；与 Claude write_gated 同一检查）。
8. **临时产物**：新生成的 `.cs`、`.md`、`.json` 等先写入 `.codex/tmp/`，不要直接写入 `.claude/`；写入 `Assets/` 按任务范围执行。

## 工作流

- Editor 状态：`unityctl status`；启动：`unityctl bridge start`（幂等）→ `unityctl editor run`
- 验证：编译通过看日志；运行时行为看 logs；视觉效果由人工在 Unity Editor/Game View 观察
- 同一错误连续 3 次修复失败 → 停下，报告人工分析

## 与 Claude 的协作

- Claude 负责：知识体系、门禁、任务编排、review、入口同步
- Codex 负责：开发任务执行（落地执行 + 自主开发全流程）、独立会话开发
- 派发约定：`.codex/INTERFACE.md` §3；模型链路：全局 `~/.codex/config.toml`（排障走 `.codex/SKILL.md`）

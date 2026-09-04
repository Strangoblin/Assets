---
name: codex-orchestrate
description: 将开发任务与基础工作派发给 Codex CLI 执行，Claude Code 保留体系裁决与门禁。Codex 直接读 .claude/ 权威源（CLAUDE.md、rules/、references/），适用于开发任务、编译错误批量修复、样板代码、批量重命名、格式化、独立小任务。涉及 Assets/Mine/ 的产出经 Claude review + 门禁链合入。触发词：codex, 派发, 编排, 批量修复, 开发, 落地, worker。链路为 OpenCode Go 直连（2026-08-17 起无本地代理）。
---

# Codex 编排 — 派发基础工作

> 单一知识体系（`.claude/`），双开发引擎。Claude Code 管体系裁决与门禁，Codex 管开发落地（同样执行开发任务，直接读 .claude/ 权威源）。

## 架构

```
Claude Code ──Bash──▶ codex exec --sandbox workspace-write "<任务>"
                        │
                        ▼
          OpenCode Go 直连（/v1/responses，无代理）
                        │
                        ▼
                    deepseek-v4-flash（config.toml 主模型）
```

Codex CLI 与 VSCode 扩展读同一个 `~/.codex/config.toml`，链路共用。

> **执行路由**：派发即路由到 **exec-developer**（`.codex/agents/exec-developer/exec-developer.md`，纯执行边界，任务书字段必填）；用户要求自主全流程时路由到 **auto-developer**（`.codex/agents/auto-developer/auto-developer.md`）。

> 🌍 **全局 skill**：本 skill 是全局的（`~/.claude/skills/`），任何项目可用。配置在 `~/.codex/`（config.toml + auth.json）也是全局的，无需每项目配置；codex exec 在未信任目录也能直接跑（自动 trust）。知识模型：Codex **直接读 .claude/ 权威源**（入口指引在 .codex/AGENTS.md，结构变更时由 codex-bridge skill 更新）。

## 前置条件（每次派发前检查）

1. **链路可用**：`codex exec "Reply with exactly: OK"` 输出 OK（直连无代理，无需守护进程）
   - ⚠️ 报 401 → 更新 `~/.codex/auth.json` 的 key；报 403 → opencode 工作台开启中国区模型
2. **Codex CLI 已装**：`codex --version`（v0.147.0，npm 官方 registry 安装）
3. **在 git 仓库内执行**（codex exec 依赖 git 工作区识别文件变更）
4. **并发安全**：多个 claude+codex 工作流可并行（直连无共享进程、配置只读共享、codex 会话独立）。唯一禁忌：同一 git 仓库内多个工作流写文件会互相冲突，应避免

## 任务分流规则

### ✅ 派发给 Codex（基础工作）

| 类别 | 示例 |
|------|------|
| **开发任务**（独立功能/修复，任务书明确边界） | "实现 X 功能 / 修复 Y bug（按任务书）" |
| 编译错误批量修复 | "修复 Assets/Mine/Scripts 下所有编译错误" |
| 样板代码生成 | "给 XXX 类生成序列化工具方法" |
| 批量文件操作 | 批量重命名、批量加注释头、批量格式化 |
| 独立小功能 | 单个工具函数、单个 shader 内小改动 |
| 跑测试/静态检查 | dotnet test、tsc --noEmit 等 |

### ❌ 留在 Claude Code（裁决与体系）

| 类别 | 原因 |
|------|------|
| `.claude/` 体系变更（宪法/rules/agents/skills 修改） | 体系裁决归 Claude（meta-developer）；Codex 可读但不可改体系 |
| 门禁裁决（Assets/Mine/ 合入） | Codex 无 MCP write_gated 通道；开发产出经 Claude review + 门禁链合入 |
| 渲染管线/架构级方案决策 | 方案设计归 Claude/auto-developer 内部；Codex 按任务书落地 |
| 涉及外部服务的操作 | 发布、推送、部署 |

## 调用模板

### 小任务（指令自包含，<2KB）

```bash
codex exec --sandbox workspace-write --skip-git-repo-check \
  "以 exec-developer 身份执行：具体、自包含的指令"
```

### 大任务（任务说明书，避免上下文截断）

Claude Code 把任务写入临时文件，Codex 读取执行：

```bash
# 1. Claude Code 写说明书
cat > /tmp/codex-task.md << 'EOF'
目标：...
涉及文件：...
约束：...
验收标准：...
EOF

# 2. 派发
codex exec --sandbox workspace-write --skip-git-repo-check \
  "以 exec-developer 身份执行：先读 .codex/agents/exec-developer/exec-developer.md，再读取 /tmp/codex-task.md 按任务书约定完整执行。不要询问，直接完成。"
```

### 指令书写要点

- **自包含**：Codex 直接读 `.claude/` 权威源（入口指引 `.codex/AGENTS.md` 列清单：CLAUDE.md 宪法、rules/、references/）；关键约束仍须写进指令（命名规范、文件路径、验收标准）
- **路由**：prompt 开头注明"以 exec-developer 身份执行"（纯执行边界）；大任务附 agent 文件路径 `.codex/agents/exec-developer/exec-developer.md`
- **明确边界**：列出允许修改的文件范围；未列出的不动
- **要求自查**：末尾加"完成后列出修改的文件清单"

## 权限红线

| 沙箱模式 | 用途 |
|----------|------|
| `--sandbox read-only` | 只查不改（代码审查、问题定位） |
| `--sandbox workspace-write` | ✅ 默认派发模式，限工作区 |
| `danger-full-access` | ⛔ 永不使用 |

Codex 的产出**必须**回到 Claude Code review（`git diff`），问题由 Claude Code 修或回退。产出合入前先跑规范检查：`python .mcp/validation/check_norm.py <file>`（与 write_gated 同一检查，exit 1 = 有 error 违规需修正）。

## 模型选择

- 默认：`deepseek-v4-flash`（config.toml 已配，需 opencode 工作台已开中国区提供商）
- 复杂任务/备选：`--model deepseek-v4-pro`
- 简单任务：`--model mimo-v2.5`（更便宜快速）

## 验证流程（派发后必做）

1. `git diff --stat` 确认改动范围符合预期
2. `git diff` 抽查关键文件
3. 不符合 → Claude Code 直接修或 `git checkout -- <file>` 回退
4. 有编译/测试条件的项目，跑编译验证

## 常见陷阱

| 陷阱 | 处理 |
|------|------|
| 指令 >2KB 被截断 | 改用任务说明书文件 |
| Codex 不了解项目规范 | 指令里指明读取 `.claude/` 权威文件（CLAUDE.md / rules/ / references/）；入口清单结构变更后由 codex-bridge 更新 |
| `Model metadata for deepseek-v4-flash not found` warning | 无害，但大任务建议在 config.toml 加 `model_context_window`/`model_max_output_tokens` 修正 fallback 元数据 |
| 直连报 401 / 403 / stream 断开 | 链路问题，走 codex-opencode-go skill 排障表 |
| codex exec 非交互输出太短 | 加上 `--sandbox workspace-write` 并明确"直接修改文件" |

## 与其他 skill 的关系

- `codex-bridge`：双边契约（.codex/INTERFACE.md）+ AGENTS.md 入口同步（低频，结构变更时）——知识本身 Codex 直接读 .claude/，无需同步
- `codex-opencode-go`：链路搭建与排障（新电脑初始化、404/403/stream 问题）
- `codex-orchestrate`：本 skill，日常派发编排。**派发前确认入口清单最新（结构变更后跑 codex-bridge 更新入口）**

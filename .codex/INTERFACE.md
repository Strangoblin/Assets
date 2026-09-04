# Codex 侧接口契约（.codex/）

> 本文件是 **Codex 侧**对外接口的权威来源。Claude 侧对应接口由 3 个 skill 组成（分工见 §1.1）：codex-bridge（契约+入口同步）、codex-orchestrate（派发编排）、codex-opencode-go（链路搭建与排障）。
> 知识模型：**单一权威源 `.claude/`，Codex 直接读取**（2026-08-25 起，不再依赖编译镜像）。双边各读各的，方向单向（.claude → .codex 入口）。

## 1. 本侧（Codex）能力声明

| 能力 | 说明 |
|------|------|
| 自动读取 | 项目根 `AGENTS.md`（trusted 目录内自动加载）→ 软链指向 `.codex/AGENTS.md`（**入口指引**，非知识全量） |
| 知识读取 | **直接读 `.claude/`**：CLAUDE.md（宪法 C1-C7）、agents/unity-developer.md（模式+退出条件）、rules/（开发规范）、agents/unity-developer/references/（知识库）、agents/unity-developer/memory/（项目上下文）。顺序：AGENTS.md 入口 → 按任务需要读 .claude/ 对应文件 |
| 不自动读取 | 无（.claude/ 需显式读取；入口已列出清单，照单读） |
| 认证 | 全局 `~/.codex/auth.json`（OpenCode Go key，chmod 600）；项目级 `.codex/auth.json` 仅 CODEX_HOME 隔离方案使用（默认不存在） |
| 链路 | 直连 `https://opencode.ai/zen/go/v1`（`wire_api = "responses"`，无代理）。**全部配置在全局 `~/.codex/config.toml`**；项目级 `.codex/config.toml` 已退役（2026-08-25 归档为 `config.toml.bak-20260825`——Codex 忽略项目级 provider 类 key） |
| 审查模型 | `review_model` 必须显式指向本区域可用模型（默认 `deepseek-v4-flash`）——auto_review 审批走独立模型，不设则提权全部 403 |
| 模型目录 | 全局 `~/.codex/model-catalogs/opencode-go.json` + config.toml `model_catalog_json`（VSCode 面板模型列表来源） |
| 门禁边界 | Codex **无 MCP 工具链**，不持有 write_gated 通道——`Assets/Mine/` 写入的门禁约束由派发方（Claude）执行：Codex 开发产出经 Claude review + 门禁链后合入 |

### 1.1 Claude 侧对应 skill 分工

| Skill | 职责 |
|-------|------|
| `codex-bridge` | 双边契约（本文档对侧）+ AGENTS.md 入口同步（.claude 变更 → 更新入口指引） |
| `codex-orchestrate` | 派发编排（codex exec 模板、沙箱、验证流程、任务书约定；开发任务与基础任务统一路由） |
| `codex-opencode-go` | 链路搭建与排障（config.toml / auth.json / 模型可用性 / 404/403/stream 问题） |

## 2. 接口文件清单

| 文件 | 角色 | 维护者 |
|------|------|--------|
| `.codex/AGENTS.md` | Codex 自动加载**入口**（指引 Codex 读 .claude/ 权威源；非知识全量、非编译镜像） | Claude 维护（低频同步） |
| `AGENTS.md`（项目根，软链） | Codex 自动读取入口 | 软链（ln -sf .codex/AGENTS.md AGENTS.md） |
| `.codex/INTERFACE.md` | 本文档：接口契约 | 双边 |
| `.codex/SKILL.md` | codex-opencode-go 接入手册的 **Codex 侧镜像**（权威源在 `.claude/skills/codex-opencode-go/SKILL.md`，同步不手改） | Claude 同步 |
| `.codex/agents/` | Codex 侧 agent：`exec-developer/`（落地执行）、`auto-developer/`（自主开发全流程） | 双边 |
| `.codex/config.toml.bak-20260825` | 项目级 config 归档（退役，勿恢复） | — |
| `~/.codex/config.toml` | 实际生效的直连配置（主模型 / review_model / provider / catalog） | 双边 |
| `~/.codex/auth.json` | API key（不入 git，chmod 600） | 双边 |

## 3. 派发约定（Claude → Codex）

- 调用：`codex exec -C <项目根> --sandbox workspace-write --skip-git-repo-check "<prompt>"`（Claude 侧由 codex-orchestrate skill 执行）
- 路由：落地执行类 → **exec-developer**（`.codex/agents/exec-developer/exec-developer.md`）；自主开发全流程 → **auto-developer**（`.codex/agents/auto-developer/auto-developer.md`）。**开发任务同样可派发 Codex**（不再限"基础重复工作"），派发时 prompt 中显式引用 agent 文件并附任务书字段（目标/涉及文件/约束/验收标准/模式）
- prompt 要求：自包含（Codex 先读 `.claude/` 权威源——CLAUDE.md 宪法、rules/、references/——再按任务书执行）、明确输出格式、限定文件范围
- 大任务（>2KB）：Claude 写任务书到 `/tmp/codex-task.md`，Codex 读取执行，避免上下文截断
- 结果：stdout 即回报内容，改动经 Claude review（git diff）；涉及 `Assets/Mine/` 的改动经门禁链确认后合入
- 沙箱红线：`--sandbox read-only`（只查）/ `workspace-write`（✅ 默认）/ `danger-full-access`（⛔ 永不使用）
- 模型：默认 `deepseek-v4-flash`（全局 config.toml 主模型，实际以全局配置为准）；复杂任务可 `--model` 覆盖

## 4. 同步规则（单向）

- 源：`.claude/`（CLAUDE.md、agents/、rules/、references/）——**权威知识源，Codex 直接读取**
- 方向：**.claude → .codex/AGENTS.md**（入口指引，仅条目变更时同步）+ `.codex/SKILL.md`（镜像），不回写
- 触发：入口条目变化（路径/清单调整）时由 Claude 更新（codex-bridge skill）；**知识内容变更无需重编译 AGENTS.md**（Codex 读源）
- Codex 侧需要新规则 → 在 `.codex/AGENTS.md` 顶部注释中标注建议，由 Claude 回写 .claude/ 源后生效

## 5. Codex 临时产物

- 新生成的 `.cs`、`.md`、`.json`、测试脚本和其他中间文件，先保存到 `.codex/tmp/` 或任务子目录。
- 不要把未经确认的新内容直接写入 `.claude/`；`.claude/` 是 Claude 侧权威知识源，由 Claude 负责归档位置、去重和同步。
- 写入 `Assets/` 的功能资产按用户任务范围和项目门禁执行。
- `.codex/tmp/` 内容不是权威知识，任务结束后由调用方审查、转移或清理。

## 5. 职责划分

| 侧 | 职责 |
|----|------|
| Claude | 知识体系、门禁、任务编排、review、入口同步 |
| Codex | **开发任务执行**（落地执行 + 自主开发全流程）、独立会话开发 |

---
name: codex-bridge
description: Codex ↔ Codex 双边桥接接口（Codex 侧）。知识模型：Codex 直接读 .Codex/ 权威源（AGENTS.md + agents + rules + references），项目根 AGENTS.md 仅作入口指引（软链 → .codex/AGENTS.md），并维护双边契约（对侧接口：.codex/INTERFACE.md）。触发词：codex, 桥接, 同步, AGENTS.md, 双边接口, 让codex读.Codex。
---

# Codex Bridge — Codex 侧接口

> 双边接口架构：Codex 侧（本文件）↔ Codex 侧（`.codex/INTERFACE.md` + `.codex/AGENTS.md`）。**单一权威源 `.Codex/`，Codex 直接读取**（2026-08-25 起）；AGENTS.md 只做入口指引，不做知识全量编译。方向单向（.Codex → .codex），各读各的。

## 架构

```
┌─────────────────────────────┐          ┌─────────────────────────────┐
│  Codex（本侧）          │  契约对应  │  Codex（对侧）                │
│  .Codex/ ←─────权威源──────┼─直接读取─▶│  自动加载：项目根 AGENTS.md    │
│   ├─ AGENTS.md               │           │    （软链 → .codex/AGENTS.md  │
│   ├─ agents/unity-developer  │           │      入口指引，非知识全量）   │
│   ├─ rules/*.md              │           │  显式读取：.Codex/ 全部权威 │
│   ├─ references/             │           │    （照入口清单）            │
│   └─ skills/codex-bridge/    │           │  .codex/SKILL.md（镜像）     │
│       SKILL.md（本文件）       │           │  .codex/agents/             │
│                              │           │  （exec/auto-developer）     │
└─────────────────────────────┘          └─────────────────────────────┘
```

> **Codex 侧 agent**：`.codex/agents/exec-developer/`（落地执行）、`.codex/agents/auto-developer/`（自主开发全流程），由 codex-orchestrate 派发时在 prompt 中显式引用。

## 核心机制：Codex 如何读 .Codex

Codex 自动加载的是**项目根 `AGENTS.md`**（trusted 目录内）。入口 = 软链到 `.codex/AGENTS.md`，内容是**读取指引**（列出 .Codex/ 权威文件清单），不再搬运知识：

```
项目根 AGENTS.md ──symlink──▶ .codex/AGENTS.md（入口指引，低频维护）
Codex 任务开始时 ──▶ 读入口 → 按清单读 .Codex/ 权威源
```

- **权威源**：`.Codex/AGENTS.md` + `.Codex/agents/unity-developer.md` + `.Codex/rules/` + `.Codex/agents/unity-developer/references/`（2026-08-24 自 `Assets/MarkDowns/` 内化）+ memory/
- **入口**：`.codex/AGENTS.md`（快速版规则 + 读取清单，仅条目变更时同步）
- **软链**：`ln -sf .codex/AGENTS.md AGENTS.md`

**好处**：消除编译镜像漂移——AGENTS.md 曾因搬运知识而落后（8-17 编译版仍指向已迁移的 `Assets/MarkDowns/`）；直接读取后 .Codex 变更即刻对 Codex 生效，无需重编译。

## 入口同步流程（.Codex 结构变更 → 更新入口）

1. 检查 `.codex/AGENTS.md` 入口清单是否与实际 .Codex/ 结构一致（目录、关键文件、路径）
2. 变更点：知识源路径、读取清单、开发规则要点、agent 路由——只在"结构变化"时改
3. 知识内容本身变更（规范条文、宪法细节）→ **不改入口**，Codex 读源自然拿到
4. 验证：软链完好 + `codex exec -C <项目根> "Reply with exactly: BRIDGE-OK"` → BRIDGE-OK

## 与 codex-orchestrate 的分工

| Skill | 职责 |
|-------|------|
| **codex-bridge**（本文件） | 双边契约 + AGENTS.md 入口同步（低频） |
| **codex-orchestrate** | 派发编排（codex exec 模板、沙箱、验证流程；开发任务与基础任务统一路由） |
| **codex-opencode-go** | 链路搭建与排障（config.toml / auth.json） |

联动：**派发前确认入口清单最新**（结构变更后先更新入口），再按 codex-orchestrate 派发——Codex 读入口 → 读 .Codex/ 权威源 → 带最新项目规则工作。

## 验证命令

```bash
# 软链状态
ls -la AGENTS.md                        # → AGENTS.md -> .codex/AGENTS.md

# Codex 端能读到入口
codex exec -C /Users/xiaokangji/Unity/Lab "Reply with exactly: BRIDGE-OK"
```

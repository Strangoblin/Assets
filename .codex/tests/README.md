# Codex 桥接链路测试集

> 验证 Claude Code → codex exec → OpenCode Go **直连**链路（2026-08-17 起无本地代理）。
> 新电脑/配置变更后跑一遍，全部 PASS 即链路健康。

## 前置条件

1. Codex CLI：`codex --version`（v0.147+）
2. 全局配置：`~/.codex/config.toml`（直连 opencode.ai/zen/go/v1）+ `~/.codex/auth.json`（chmod 600）
3. 链路自检：`codex exec "Reply with exactly: OK"` → 输出 OK

## 测试矩阵

| # | 名称 | 能力维度 | 沙箱 | 通过标准 |
|---|------|---------|------|---------|
| 01 | smoke | 链路冒烟 | read-only | 输出 `LINK_OK` |
| 02 | readonly | 只读代码审查 | read-only | 输出包含审查结论 |
| 03 | write | 写文件 | workspace-write | output/ 生成 HelloWorld.cs 且内容匹配 |
| 04 | batch | 批量多文件 | workspace-write | output/ 生成 3 个文件 |
| 05 | spec | 任务说明书传递 | workspace-write | 产出符合说明书要求 |

## 运行

```bash
./run_all.sh            # 全部测试
./run_all.sh 03         # 单个测试
```

## 目录约定

- `task.md` — 派发给 codex 的指令（自包含）
- `expected.md` — 预期结果
- `verify.sh` — 自动验证（输出 PASS/FAIL）
- `output/` — 写操作目标（测试产物，可清理）

⚠️ 测试写文件只允许在各自 `output/` 目录内，不允许触碰项目源码。
⚠️ `run.log` / `output/` 为测试产物，不入 git。

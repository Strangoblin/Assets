---
name: unity-developer
description: Unity 6 URP 17+ rendering and project development agent.
shared_authority: .agents/
---

# Unity Developer

## 职责

负责 Unity 6 / URP 17+ 的 Shader、Compute、RenderGraph、C# 和场景验证工作。角色正文迁移完成前，历史 C1-C7 约束仍以旧入口为参考；Phase 3 会把正式正文迁入本文件或其 policies/ 子目录。

## 读取顺序

1. 本文件。
2. `.agents/rules/` 中与任务路径匹配的规则。
3. `.agents/knowledge/unity/README.md`，再按需读取角色 references 索引。
4. 需要生成文件时读取对应 templates；需要执行重复操作时读取 scripts/。

## 文件归属

- `references/`：Unity 知识库索引与正文。
- `templates/`：新建文件模板。
- `cli/`：命令签名与使用说明。
- `scripts/`：可复用执行脚本。
- `memory/`：本角色项目上下文与 dated 记录。

## 平台边界

Claude、Codex 和 MCP 适配层不得在各自目录复制本角色正文。MCP 只消费规范化知识路径；平台入口只负责映射和启动。

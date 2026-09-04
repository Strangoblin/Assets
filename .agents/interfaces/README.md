# 跨平台接口索引

`.agents/interfaces/` 定义所有平台都能理解的路径、知识 ID 和适配边界。Claude/Codex 的客户端配置不迁入此处；它们只引用这里的契约。

| 契约 | 内容 |
|---|---|
| `knowledge-paths.md` | 知识 ID、规范路径与 project-root resolver |

平台专属接口仍分别保留在 `.claude/`、`.codex/`；执行实现仍保留在 `.mcp/`。

# 领域知识索引

`.agents/knowledge/` 是路由索引层，不是第二个正文副本。每个领域索引应指向角色归属的 references 或共享 rules。

## ID 约定

知识 ID 使用 `domain/<canonical-relative-path>`，例如 `unity/standard/shader/shader-structure.md`。ID 不使用 basename，不包含绝对路径，不因平台改变。

## 读取规则

1. 先读取领域索引。
2. 由索引解析到唯一规范路径。
3. 缺失、重复或命中兼容壳时失败，不猜测替代文件。
4. MCP gate 的回执应记录解析后的仓库相对路径。

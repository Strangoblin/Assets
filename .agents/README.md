# `.agents/` 共享核心

> 本目录是项目级 agent 体系的跨平台共享权威源（SSOT）。Claude、Codex 和其他接入方都应从这里读取共享角色、规则、知识索引与 skills。

## 所有权

- `.agents/agents/`：角色定义及其职责范围内的 references、templates、CLI、scripts、memory。
- `.agents/rules/`：与客户端无关的项目硬规则。
- `.agents/knowledge/`：按领域组织的知识路由索引，不复制知识正文。
- `.agents/interfaces/`：跨平台路径、知识 ID 和适配契约。
- `.agents/skills/`：共享 skills 的唯一编辑位置。
- `.claude/`、`.codex/`：平台适配层；`.mcp/`：执行门禁基础设施。

## 单源规则

1. 跨平台内容只能在 `.agents/` 保留一个可编辑权威副本。
2. 平台目录只能保留启动入口、配置、hooks、测试和兼容适配；禁止手工复制共享正文。
3. 需要兼容旧路径时，只能使用明确标注的薄壳、软链或可验证生成物。
4. 新增共享内容前，先检查目标层索引与全库同名文件，避免重复权威源。
5. 所有仓库内路径使用 POSIX 风格相对路径；用户目录只能由外部配置注入。

## 迁移状态

当前为 Agent 架构重构 Phase 3：共享 rules、角色定义和角色归属内容已迁入；旧 `.claude` 发现路径保留相对软链兼容壳。skills 漂移裁决、MCP/Claude/Codex 切流和旧副本清理仍在后续阶段执行。

## 读取顺序

1. 根 `AGENTS.md`。
2. 角色入口：`.agents/agents/<role>/AGENT.md`。
3. 任务相关的 `.agents/rules/` 和 `.agents/knowledge/<domain>/` 索引。
4. 按索引读取具体 reference、template、script 或 memory；不要扫描整个目录。

角色更名、长文档拆分和旧副本清理不属于本阶段。

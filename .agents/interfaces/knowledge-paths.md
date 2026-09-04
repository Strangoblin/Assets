# Knowledge path contract

## Knowledge ID and canonical form

- 仓库内路径统一使用 POSIX 分隔符和相对仓库根的路径。
- 稳定知识 ID 采用 `domain/<canonical-relative-path>`。
- Unity 示例：`unity/standard/shader/shader-structure.md`。
- 解析后必须得到唯一、存在的文件；basename 只能用于人工搜索，不能作为 gate 的唯一键。
- 兼容壳、生成镜像和历史快照不得成为 canonical path。

## Project-root resolver

实现方按以下顺序解析项目根，禁止写死用户机器绝对路径：

1. 从当前适配器、配置或脚本文件位置向上查找包含 `.agents/` 和 `AGENTS.md` 的目录。
2. 若仓库可用，使用 `git rev-parse --show-toplevel` 并校验结果包含 `.agents/`。
3. 若调用方提供 `PROJECT_ROOT`，仅在其通过上述结构校验后使用。
4. 无法唯一确定根目录时失败，并返回需要修复的路径信息。

## Resolver output

resolver 返回：`project_root`、`knowledge_id`、`canonical_relative_path` 和 `resolved_path`。日志与 gate 审计只记录仓库相对路径；绝对路径仅用于进程内文件访问。

## Migration rule

Phase 2 只发布契约，不切换 `.mcp`、Claude 或 Codex 的读取根。Phase 4 负责实现解析器并加入缺失、重复、旧路径和跨平台路径测试。

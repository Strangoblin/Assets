# MCP 门禁工具速查（unity-gate server，后果验证 v2）

> 本项目的写入门禁系统。**任何 `Assets/Mine/` 写入必须走 `write_gated`**——原生 Write/Edit 已被 settings deny。
> 会话内工具名带前缀：`mcp__unity-gate__gate_pass`、`mcp__unity-gate__write_gated` 等（就是本页的工具）。
> 权威来源：`.mcp/gate_center.py`（配方表/注册表）、`.mcp/validation/norms.py`（规范规则）、`.mcp/README.md`（架构）、`.mcp/tests/test_recipes.py`（行为测试）。

## v2 设计（2026-08-25 收敛）

门禁从「流程仪式」（5 门禁链校验自报告）收敛为「**知识证据 + 内容后果验证**」：

```
gate_set_recipe → gate_pass(g_entry) → gate_pass(g_knowledge) → write_gated(...)
     │                │                       │                      │
  模式声明         身份确认          知识证据（声明必须命中真实文件）  内容规范检查（error 阻断）
```

- **链唯一**：所有配方 = `[g_entry, g_knowledge]`。g_mode/g_script/g_file/g_web_search/g_plan 已退役（调用返回 GATE_NOT_IN_RECIPE）
- **g_knowledge 防编造**：声明的 loaded_files 必须解析到 references/、rules/ 真实 .md，或项目内真实文件（参考实现）
- **write_gated 验后果**：对写入内容执行结构规范检查，error 级违规阻断，warning 级提示放行
- **write_gated 原子写**：同目录隐藏临时文件（`.` 前缀，Unity 忽略）+ rename——Editor 只看到完整新内容，无中间混合态（瞬时编译错误 / mtime 误报）
- **Codex 对等**：同一检查有 CLI 通道（见文末），Codex 产出合入前自查

## 工具

| 工具 | 作用 | 何时调用 |
|------|------|---------|
| `gate_status()` | 当前配方 + 已过门禁 + 剩余 + 写入审计 | **任何任务第一步**（确认当前状态） |
| `gate_set_recipe(name)` | 声明模式：Production/Research/Experiment/Debug/Minimal/Quick | 新任务第一步 |
| `gate_pass(gate_id, ...)` | 通过 g_entry 或 g_knowledge | 各调一次 |
| `gate_reset()` | 清空状态 | 切换任务时 |
| `gate_list()` | 列出所有门禁 + 配方（含退役标记） | 不确定时查 |
| `script_list()` | 列出 scripts/roslyn/ 可用脚本 | 需要脚本决策时 |
| `write_gated(path, content, mode=, script_decision=, file_type=, category=, effect=)` | **唯一写入通道**（门禁 + 规范检查通过才放行） | 写入时 |

## 标准流程（3 步）

```
1. gate_set_recipe("Production")   ← 新任务先声明模式（旧状态会清空）
2. gate_pass(g_entry, agent="unity-developer")
   gate_pass(g_knowledge, loaded_files="unity/standard/shader/shader-structure.md, unity/standard/script/script-structure.md", status="COMPLETE")
3. write_gated(path, content)      ← 全量文件内容；成功后可继续写（门禁状态保留）
```

## g_knowledge 铁律

- **先真实读取**对应知识文件，再申报 `status="COMPLETE"`——申报不是仪式，声明条目会被解析校验
- 高优先级必读：`unity/standard/shader/shader-structure.md`（shader 写入）+ `unity/standard/script/script-structure.md`（C# 写入），两者都声明
- 参考实现等代码文件用项目相对路径声明（如 `Assets/Mine/Shaders/Render/PBRToon/PBRToon.shader`）
- 编造文件名 → `G15_UNRESOLVED_FILE` DENIED

## 后果验证规则（norms.py）

| 规则 | 适用 | 级别 | 检测 |
|------|------|------|------|
| `shader-decl` — 必须含 `Shader "..."` 声明 | .shader | **error 阻断** | 全量 |
| `cs-type-decl` — 必须含类型声明 | .cs | **error 阻断** | 全量 |
| `region-added` — 禁止 #region | .cs | **error 阻断** | 仅新增行 |
| `divider-added` — 不用 `// ---`/`// ===` 纯分隔线 | .shader/.hlsl/.cs | warning 提示 | 仅新增行 |

- **新增行 diff**：对已存在文件只检查本次写入引入的行——历史遗留不合规（旧文件早于规范）不阻断新写入
- error 违规 → `NORM_VIOLATION` DENIED，violations 带规范来源；warning → 放行，响应携带 `warnings`

## 注解（非阻塞，记录审计）

write_gated 可选参数，记录在响应 `annotations` + `gate_status` 写入审计中：

- `mode` — 缺省取当前配方
- `script_decision` — 复用脚本库校验（`USE scene-query.cs` / `CREATE reusable x.cs` / `NONE`），非法值不阻断只记录
- `file_type` / `category` / `effect` — 自由文本分类

## DENIED 响应解读

| error | 含义 | 处理 |
|-------|------|------|
| `NO_RECIPE` | 没声明模式 | 先 `gate_set_recipe` |
| `GATE_NOT_IN_RECIPE` | 调了退役门禁（g_mode/g_script/g_file/...） | 模式走 recipe，脚本/分类走 write_gated 注解 |
| `GATE_NOT_PASSED` | 写入被拒 | `missing` 列出缺的门禁，补过再写 |
| `G15_UNRESOLVED_FILE` | 声明条目不存在 | 对照 references/ 或项目实际路径修正 |
| `NORM_VIOLATION` | 内容违反结构规范 | 按 violations[].source 修正后重试 |
| `INVALID_RECIPE` / `INVALID_GATE` | 名字拼错 | 对照 gate_list |

## Codex 对等通道

```bash
python .mcp/validation/check_norm.py <file>   # exit 0 = 通过; exit 1 = 有 error 违规
```

Codex 产出合入前自查；Claude merge review 复查同一检查（与 write_gated 的新增行检查互补，CLI 为全量）。

## 提醒

- 门禁状态持久化于 `.mcp/state.json`（进程内 + 落盘双份）——**server 进程空闲重启后自动恢复**，无需重走链。新任务第一件事仍是 `gate_set_recipe()` 显式声明；`gate_reset()` 会清空持久化。若遇 `NO_RECIPE`：先 `gate_status()` 确认真空，再 `gate_set_recipe()` 重走链
- 错误/行为疑问先跑 `.mcp/tests/test_recipes.py`（All tests passed 为准）

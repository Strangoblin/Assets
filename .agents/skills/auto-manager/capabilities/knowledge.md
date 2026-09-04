# knowledge — 知识库预加载

> 从 `agents/unity-developer/references/`（知识库，2026-08-24 自 Assets/MarkDowns/ 迁移，2026-08-24 归入 unity-developer agent）加载项目知识库，提取关键约束注入上下文。

---

## 预加载逻辑

```
AutoMode 激活
  │
  ├── [0.1] 扫描知识库 agents/unity-developer/references/*/README.md（索引）
  │     └── 原 Assets/MarkDowns/ → .claude/references/（2026-08-24 内化）→ agents/unity-developer/references/（2026-08-24 归属）
  │
  ├── [0.2] 按优先级读取（路径均为 agents/unity-developer/references/ 下）
  │     ├── 高优先级（必读）：结构规范类 → csharp-dev/script-structure.md, urp-shader-lib/shader-structure.md
  │     ├── 中优先级（按需）：模板类 → ../../templates/shader-doc-template.md, ../../templates/script-doc-template.md
  │     └── 低优先级（相关时读）：领域知识类 → unity6-api/postprocess-differences.md 等
  │
  └── [0.3] 提取关键约束
        ├── 代码风格规范（命名、注释格式、文件结构）
        ├── Shader 语法差异（Unity 6 vs 旧版本的 API 变化）
        └── 模板要求（新建文件时应遵循的文档格式）
```

## 读取策略

| 条件 | 行为 |
|------|------|
| references 目录不存在 | 跳过，不报错 |
| 文件已在当前会话中读过 | 跳过（避免重复消耗 context） |
| 文件 > 500 行 | 先读前 100 行确认内容，再决定是否全读 |
| 用户指令明确涉及 Shader | 必读 urp-shader-lib/shader-structure.md + templates/shader-doc-template.md |
| 用户指令明确涉及 C# 脚本 | 必读 csharp-dev/script-structure.md + templates/script-doc-template.md |
| 用户指令涉及全屏后处理 | 必读 unity6-api/postprocess-differences.md |
| 生产模式（全量） | 高优先级全部必读，不按任务类型裁剪 |

## 约束应用

读取完毕后，后续所有代码生成和修改必须遵循内化知识库（references/）中定义的规范：

1. **脚本结构**：遵循 `script-structure.md` 中的文件组织方式
2. **Shader 结构**：遵循 `shader-structure.md` 中的语法和 API 用法（特别注意 Unity 6 差异）
3. **文档模板**：新增文件时按 `script-doc-template.md` / `shader-doc-template.md` 格式添加头部注释

## 参考实现跟进 ⚠️

内化规范文件（references/）头部可能声明了参考实现。**如果当前任务的目标文件类型匹配参考实现的类型，必须读取至少 1 个参考实现文件。**

```
读取 references/ 规范
  │
  ├── 检查文件头部是否有 "> 参考实现：" 块
  │     │
  │     ├── 有 → 匹配任务类型 → 读取同类型参考
  │     │     ├── 全屏后处理 Shader → 读 SSSM.shader 或 SSO.shader
  │     │     ├── 普通 Shader → 读 PBRToon.shader
  │     │     └── C# Feature → 读 SSSMFeature.cs 或 SSOFeature.cs
  │     │
  │     └── 没有 → 跳过
  │
  └── 目的：规范是抽象规则，参考实现是具体范例。两者结合才能正确理解"好的代码应该长什么样"。
```

## agents/unity-developer/references/ — 知识库

> agent 的 references/ 目录在 unity-developer 激活时可用。MD 文件为目录索引，实际内容（.hlsl / .shader / .compute / .cs）带有详细注释。
> 所有 Shader / C# 开发必须优先查阅此目录。

```
写 Shader / Compute
  │
  ├── [R1] 先读 references/urp-shader-lib/README.md（索引）
  │     ├── shader-structure.md      → 普通/后处理 Shader 结构规范（高优先级）
  │     ├── blit-fullscreen.md        → Unity 6 Blitter 全屏 Shader 模式
  │     ├── hlsl-includes.md          → include + CBUFFER 速查
  │     └── compute-shader.md          → Compute Shader + Metal
  │
  ├── [R2] 复制模板: ../../templates/
  │     ├── fullscreen-postprocess.shader  → 全屏后处理 (基于 Blit.hlsl)
  │     ├── compute-template.compute       → Compute Shader
  │     └── urp-renderpass.cs              → C# RenderGraph Pass
  │
  └── [R3] 抄 API: references/unity6-api/
        ├── postprocess-differences.md → Unity 6 vs 2022 全屏后处理差异
        ├── render-graph.md           → RecordRenderGraph 标准写法
        ├── blitter-api.md            → Blitter.BlitTexture API
        ├── compute-shader-api.md     → ComputeShader C# dispatch
        ├── rthandle-api.md           → RTHandle 生命周期
        └── volume-component.md       → VolumeComponent 参数定义
```

### 查阅策略

| 条件 | 行为 |
|------|------|
| 写全屏后处理 Shader | 必读 `shader-structure.md` + `blit-fullscreen.md` + 复制模板 `fullscreen-postprocess.shader` |
| 写 Compute Shader | 必读 `compute-shader.md` + 复制模板 `compute-template.compute` |
| 写 C# RenderGraph Pass | 必读 `render-graph.md` + 复制模板 `urp-renderpass.cs` |
| Metal 平台问题 | 查阅 `platform/metal-notes.md` |
| API 签名不确定 | 查阅 `unity6-api/` 子目录，不凭记忆 |

### 知识库分层（2026-08-24 归 agent 后）

| 层 | 路径 | 内容 | 加载方式 |
|---|------|------|---------|
| 硬规则 | rules/*.md（顶层，路径门控） | 必须遵守的强制规范 + 错误诊断 | 自动注入 |
| 知识库 | agents/unity-developer/references/urp-shader-lib/, csharp-dev/, unity6-api/ | 结构规范、领域知识、API 差异 | agent 激活时可用，按需读取 |
| 模板 | agents/unity-developer/templates/ | 新建文件复制的模板 | 按需复制 |

---

## 模式差异

| 模式 | 知识加载策略 |
|------|-------------|
| 研发模式 | 按需加载（涉及 Shader → 读 Shader 相关；涉及 C# → 读 Script 相关） |
| 生产模式 | 全量预加载（自动扫描并读取所有高优先级文件） |

---

## [G1.5] Knowledge Check 判定标准

> 门禁定义见 SKILL.md，各模式必经。生产模式强制阻断。

| Status | 判定条件 | 行为 |
|---|---|---|
| COMPLETE | 高优先级文件（结构规范类 2 份）已全部读取 | 进入下一步 |
| — | 未全读 | 阻断进入 P2，补读后重新输出 COMPLETE（门禁只收 COMPLETE，PARTIAL 已废弃） |

# knowledge — 知识库预加载

> 从 `.agents/agents/unity-developer/references/`（知识库，2026-08-24 自 Assets/MarkDowns/ 迁移，2026-08-24 归入 unity-developer agent）加载项目知识库，提取关键约束注入上下文。

---

## 预加载逻辑

```
AutoMode 激活
  │
  ├── [0.1] 扫描知识库 .agents/agents/unity-developer/references/*/README.md（索引）
  │     └── 原 Assets/MarkDowns/ → .agents/agents/unity-developer/references/（2026-08-24 内化）→ .agents/agents/unity-developer/references/（2026-08-24 归属）
  │
  ├── [0.2] 按优先级读取（路径均为 .agents/agents/unity-developer/references/ 下）
  │     ├── 高优先级（必读）：结构规范类 → standard/script/script-structure.md, standard/shader/shader-structure.md
  │     ├── 中优先级（按需）：模板类 → ../../agents/unity-developer/templates/standard/ and ../../agents/unity-developer/templates/script/、../../agents/unity-developer/templates/shader/
  │     └── 低优先级（相关时读）：领域知识类 → shader/postprocess/ 与 standard/*
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
| 用户指令明确涉及普通 Shader | 必读 standard/shader/shader-structure.md + .agents/agents/unity-developer/templates/standard/shader/shader-doc-template.md |
| 用户指令明确涉及纯 C# 脚本 | 必读 standard/script/script-structure.md + .agents/agents/unity-developer/templates/script/ |
| 用户指令涉及全屏后处理 | 必读 shader/postprocess/fullscreen-structure.md + postprocess-differences.md |
| 用户指令涉及 RendererFeature / RenderPass | 必读 shader/postprocess/feature-script-structure.md + standard/rendering/render-graph.md |
| 生产模式（全量） | 高优先级全部必读，不按任务类型裁剪 |

## 约束应用

读取完毕后，后续所有代码生成和修改必须遵循内化知识库（references/）中定义的规范：

1. **标准脚本结构**：遵循 `standard/script/script-structure.md` 中的文件组织方式
2. **标准 Shader 结构**：遵循 `standard/shader/shader-structure.md` 中的语法和 API 用法
3. **功能集成结构**：后处理 Feature 使用 `shader/postprocess/` 下的专项规范
4. **文档模板**：按实际功能选择 `.agents/agents/unity-developer/templates/standard/`、`.agents/agents/unity-developer/templates/script/` 或 `.agents/agents/unity-developer/templates/shader/` 下的模板

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

## .agents/agents/unity-developer/references/ — 知识库

> References are classified by actual responsibility. Artifact extensions (`.cs`, `.shader`, `.compute`, `.md`) do not determine ownership.

```
写普通 Script
  └── [R1] .agents/agents/unity-developer/references/standard/script/
        └── script-structure.md

写普通 Shader / HLSL
  └── [R1] .agents/agents/unity-developer/references/standard/shader/
        ├── shader-structure.md
        └── hlsl-includes.md

写 Compute / RenderGraph 基础
  └── [R1] .agents/agents/unity-developer/references/standard/compute/ 或 standard/rendering/

写后处理 Shader 功能
  ├── [R1] .agents/agents/unity-developer/references/shader/postprocess/
  │     ├── fullscreen-structure.md
  │     ├── blit-fullscreen.md
  │     ├── postprocess-differences.md
  │     └── feature-script-structure.md
  │
  ├── [R2] 复制模板: ../../agents/unity-developer/templates/shader/postprocess/
  │     ├── fullscreen-postprocess.shader
  │     ├── urp-renderpass.cs
  │     ├── volume-template.cs
  │     └── compute-template.compute
  │
  └── [R3] 跨领域平台知识: .agents/agents/unity-developer/references/platform/metal-notes.md
```

### 查阅策略

| 条件 | 行为 |
|------|------|
| 写普通 Shader | 读取 `standard/shader/`，按需复制 `.agents/agents/unity-developer/templates/standard/shader/` |
| 写纯 C# 脚本 | 读取 `standard/script/`，按职责检查 `.agents/agents/unity-developer/templates/script/` |
| 写全屏后处理 Shader | 读取 `shader/postprocess/fullscreen-structure.md` + `blit-fullscreen.md` + 复制后处理 Shader 模板 |
| 写 RendererFeature / RenderPass | 读取 `shader/postprocess/feature-script-structure.md` + `standard/rendering/render-graph.md` |
| 写屏幕空间 Compute | 读取 `shader/postprocess/compute-shader.md` + 复制后处理 Compute 模板 |
| Metal 平台问题 | 查阅 `platform/metal-notes.md` |
| API 签名不确定 | 查阅 `standard/compute/` 或 `standard/rendering/`，不凭记忆 |

### 知识库分层（2026-09-03 按实际职责整理）

| 层 | 路径 | 内容 | 加载方式 |
|---|------|------|---------|
| 硬规则 | rules/*.md（顶层，路径门控） | 必须遵守的强制规范 + 错误诊断 | 自动注入 |
| 标准知识库 | .agents/agents/unity-developer/.agents/agents/unity-developer/references/standard/ | 跨功能的 Script / Shader / Compute / Rendering 基础 | 按需读取 |
| Shader 功能知识库 | .agents/agents/unity-developer/.agents/agents/unity-developer/references/shader/ | 后处理等渲染功能及其混合文件类型知识 | 按功能读取 |
| 模板 | agents/unity-developer/.agents/agents/unity-developer/templates/standard/、script/、shader/ | 新建文件复制的模板 | 按需复制 |

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

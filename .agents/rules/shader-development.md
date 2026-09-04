---
paths:
  - "Assets/Mine/Shaders/**"
  - "**/*.shader"
  - "**/*.hlsl"
---
# Shader 开发规范

> 完整结构规范见 [references/standard/shader/shader-structure.md](../agents/unity-developer/references/standard/shader/shader-structure.md) and [references/shader/postprocess/fullscreen-structure.md](../agents/unity-developer/references/shader/postprocess/fullscreen-structure.md)（2026-08-24 内化，归属 unity-developer）

## Include 顺序（固定，不可调换）

```hlsl
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
// 项目自定义 include 放最后
```

- **依赖前置**：自定义 include 内引用的全局（`#define` 常量、`static const` 数组、CBUFFER 参数）必须在 include 行**之前**声明——`#include` 是线性文本展开，后置定义会报 `undeclared identifier`（2026-08-27 StarryNight 实坑：SDF_POINT_COUNT）
- **调用先于定义**：HLSL 函数调用者必须先于被调用者定义——Unity/Metal 编译管线不支持下向声明，前向引用报 `undeclared identifier`（2026-09-01 StarryNight 实坑：ComputeRowColor 调用文件后部定义的 Hash2 → 编译失败，修复 = Hash/Hash2 置于文件顶部）

## 全屏后处理必须项

- `Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }`
- `Cull Off ZWrite Off ZTest Always`
- `#pragma target 2.0`（Metal 强制）
- 纹理采样用 `SAMPLE_TEXTURE2D_X`，非 `SAMPLE_TEXTURE2D`
- 纹理声明用 `TEXTURE2D_X`，非 `TEXTURE2D`
- 输入纹理名为 `_BlitTexture`，非旧版 `_MainTex`
- Vertex shader 用 Blit.hlsl 提供的 `Vert()`，不手写

## Metal 兼容

- 所有 vertex output 字段必须显式初始化
- `#pragma target 2.0` 必须声明
- 不在 frag shader 中大量使用 `clip()`（会导致 GPU 崩溃）

## 项目约定

- 文件头必须有 `// ═══` 分隔注释块
- Pass 命名用 PascalCase，与功能对应
- 公共函数/结构体提取到 `.hlsl` 文件，不复制粘贴

## ddx/ddy 基底陷阱

- **铁律**：同一个 Jacobian 的所有项必须**同基底**。屏幕基底（ddx/ddy）与固定步长差分（世界基底）混用 → 相机因子约不掉，随视距/视角明暗漂移
- 详情（症状/做法/分析）：[memory/2026-08-04-water-caustics-screen-independent.md](../agents/unity-developer/memory/2026-08-04-water-caustics-screen-independent.md)

## 常见错误诊断

| 症状 | 可能原因 | 诊断 |
|------|---------|------|
| 渲染无效果 | Shader 未绑定 / RenderGraph 纹理未连接 | 检查 Material.SetShader / builder.UseTexture |
| `_BlitTexture` 采样全黑 | 忘记 include Blit.hlsl | 检查 `#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"` |
| Metal 编译失败 | 缺少 `#pragma target 2.0` | 加在 HLSLPROGRAM 内第一行 |
| GPU crash | frag 中大量 `clip()` | Metal 上避免，用 alpha 替代 |
| GetShaderMessages 返回上次编译缓存 | 写入后编辑器尚未重编译 | 等自动导入完成后查询，或先 AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate) 再查——否则旧编译错误会被误判为"干净"（2026-09-01 StarryNight 实坑） |

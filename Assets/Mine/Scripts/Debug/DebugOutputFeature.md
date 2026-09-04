# DebugOutputFeature

通用全屏 Shader 调试 RendererFeature。它不输出场景图，也不单独绘制某个场景物体，而是把绑定的全屏 Shader 直接覆盖到真实相机颜色目标。

## 职责边界

- Feature：只负责根据 Shader 创建内部材质、RenderGraph 临时 RT、全屏 Blit 和屏幕输出。
- Shader：负责所有实际图像内容、参数和理论模型；Feature 不暴露冗余 Material 字段。
- 不复制被测 Shader 的射线、求交、映射或颜色合成算法。

## InteriorMapping 检查

`InteriorMappingScreenDebug.shader` 通过 include 共用 `InteriorMappingFunction.hlsl`，把单房间映射模型直接应用到 2D 全屏 UV。这样输出只包含 Shader 图像，RT 其它内容不来自场景物体。

## 验证方式

在 Renderer Data 中添加 `DebugOutputFeature`，Inspector 指定 `debugShader` 并勾选 `settings.debug`，Editor Game View 直接观察全屏输出。验证完成后取消勾选 `debug` 并关闭 Feature Active。

调试方法与失败定位顺序（编译检查 → snapshot → logs → Game View）见 [script-structure.md](agents/unity-developer/references/csharp-dev/script-structure.md)「屏幕调试方法」。像素抽查应在全屏 Shader 输出 RT 上进行，并与同一组 Shader 参数的理论值对比。

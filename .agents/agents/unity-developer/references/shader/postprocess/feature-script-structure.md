# Postprocess Feature Script Structure

> `ScriptableRendererFeature` + `ScriptableRenderPass` guidance for shader/postprocess features.
> Shared MonoBehaviour and utility conventions: [standard script structure](../../standard/script/script-structure.md).

## Feature / RenderPass 脚本结构参考

适用于 `ScriptableRendererFeature` + `ScriptableRenderPass` 组合的后处理脚本。

### 1. 整体布局

```csharp
public class XxxFeature : ScriptableRendererFeature
{
    [Serializable]
    public class Settings
    {
        // ── 面板参数 ──
        public Type param;

        // ── PropertyToID 预处理 ──
        internal static readonly int ParamID = Shader.PropertyToID("_Param");
    }

    class XxxPass : ScriptableRenderPass
    {
        class PassData { /* RenderGraph 传递数据 */ }

        public override void RecordRenderGraph(RenderGraph rg, ContextContainer frameData)
        {
            // 1. 材质参数设置
            // 2. 关键字切换
            // 3. 创建临时 RT
            // 4. 编排 Pass 调用顺序
        }
    }

    public Settings settings = new();
    public override void Create() { ... }
    public override void AddRenderPasses(...) { ... }
}
```

### 2. 关键规范

- `PropertyToID` 在 `Settings` 内以 `static readonly int` 预计算
- 每个 Feature 配一个 Debug 开关，控制中间结果可视化
- `ConfigureInput` 声明所需纹理（Color / Depth / Normal）
- RenderGraph 模式下使用 `UnsafePass` + `SetRenderFunc` + `PassData`

### 3. 文件组织

```
Assets/Mine/Shaders/XxxPostEffect/
  ├── XxxPostEffect.shader       — Shader 代码
  ├── XxxFeature.cs              — C# Feature + Pass
  └── XxxPostEffect.md           — 技术文档
```

### 4. 屏幕调试方法（RendererFeature 屏幕调试）

后处理 Feature 的验证方式：在真实 RendererFeature / RenderGraph 中把中间结果直接 Blit 到相机颜色目标，人工观察 Editor Game View。替代 Probe 场景夹具、复制 shader、`ReadPixels` 像素回读和外部数学复算（2026-08-25 决策）。

**Feature 侧约定**

- 提供可序列化的 Debug 开关（枚举 `Off / Trace / Resolve` 或布尔），Debug 分支复用实际生成的中间纹理，不建第二套测试管线：

```csharp
public enum DebugMode { Off, Trace, Resolve }
[Header("Debug")] public DebugMode debug = DebugMode.Off;

if (debug != DebugMode.Off)
    Blitter.BlitCameraTexture(cmd, intermediate, cameraColor);
else
    Blitter.BlitCameraTexture(cmd, final, cameraColor);
```

**调试步骤**

```bash
# 1. 刷新并确认 shader 编译
unityctl asset refresh
unityctl script eval -u UnityEditor '...ShaderUtil.GetShaderMessages...'

# 2. 加载真实场景，打开目标 Feature 的 Debug 模式
unityctl scene load Assets/Scenes/SampleScene.unity

# 3. 运行真实相机管线
unityctl logs clear
unityctl play enter
unityctl snapshot
unityctl logs --count 80 --level error --stack

# 4. 在 Editor Game View 人工观察 debug 画面
unityctl play exit
```

**失败定位顺序**

1. `ShaderUtil.GetShaderMessages`：shader 是否编译成功
2. `snapshot`：目标相机、Renderer Data、Feature 是否实际存在并启用
3. `logs --level error --stack`：RenderGraph、资源输入或运行时异常
4. Game View：debug RT 是否有有效画面，是否被后续 pass 覆盖
5. 仅在 Feature 本身缺少中间输出时，才补充 debug pass；不要退回 Probe 场景或 Pixel Probe 回读

**固定调试 Feature**

调试独立全屏 Shader（非 Feature 自带 Debug）时，使用通用 [DebugOutputFeature](Assets/Mine/Scripts/Debug/DebugOutputFeature.md)：绑定 Shader 直接覆盖到真实相机颜色目标，Inspector 指定 `debugShader` + 勾选 `settings.debug` 即可，无需临时 Roslyn 脚本。

**提醒**：调试开关是编辑器资产修改，验证结束后恢复 `Debug=Off` 和原始 Active 状态；不要把调试开关作为最终画面配置提交。


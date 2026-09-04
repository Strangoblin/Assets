# SSL — Screen Space Light

> 纯后处理（RendererFeature + settings，无 Volume）。2026-08-27 解耦：参数从 Volume 回落 Feature Settings。
> Feature 接入 Volume 的通用方法已归档，恢复 Volume 驱动模式见文末。

---

## 概述

SSL（Screen Space Light）是一个全屏后处理效果，支持两种模式（`Settings.sslMode`，对应 Shader 关键字）：

| 模式 | 关键字 | 算法 | 用途 |
|------|--------|------|------|
| `RAY3D_Fog` | `SSL_RAY3D` + `SSL_FOG` | 沿视线 3D 步进，累积深度密度 | 体积雾 |
| `RAY3D_Light` | `SSL_RAY3D` + `SSL_LIGHT` | 沿视线 3D 步进，采样阴影贴图 + HG 相位函数 | 屏幕空间体积光（God Ray） |
| `RBR2D` | `SSL_RBR2D` | 全屏径向模糊采样累积（24 samples + jitter） | 径向模糊 / 光晕拖尾 |

> RAY3D = 原 SSL 方法；RBR2D = 2026-08-27 加入，改编自 Shadertoy "Full Scene Radial Blur"（Passion / IQ / mu6k），
> **blur 中心从屏幕中心改为主平行光方向在屏幕上的投影焦点**，模糊方向即平行光方向。

---

## 管线结构

```
Pass 0  SSL_Raymarch   模式分发：RAY3D 步进 / RBR2D 径向模糊 → blurPing[0]（全分辨率）
Pass 1  SSL_BlurHorizontal   多级 blur 链（BlurFunction.hlsl）
Pass 2  SSL_BlurVertical     多级 blur 链
       （blurLevels>0 时 Ping/Pong 降采样 + 上采样；blurLevels=0 跳过）
       cmd.SetGlobalTexture("_SSLTex", blurPing[0])   ← 供其他 Shader 复用
Pass 3  SSL_Mix        合成：RBR2D → lerp(main, ssl, _Intensity)；RAY3D → main + ssl
Debug   SSLFeature=true 时直接把 _SSLTex 输出到屏幕
```

- RenderPass: `BeforeRenderingPostProcessing`，`ConfigureInput(Color | Depth)`
- 临时 RT: ARGBHalf，RenderGraph 自动管理

## 参数（Feature Settings → Shader Uniform）

| Settings 字段 | Shader Uniform | 范围 | 说明 |
|---|---|---|---|
| maxSteps | `_MaxSteps` | 1-256 | RAY3D 步进次数 |
| maxDistance | `_MaxDistance` | 0.1-100 | RAY3D 步进距离上限；RBR2D 焦点投影距离 |
| intensity | `_Intensity` | 0-5 | RAY3D 光强度；RBR2D mix 混合系数 |
| sslScale | `_SSLScale` | 0-2 | RAY3D HG 相位参数（-g 各向异性） |
| jitterScale | `_JitterScale` | 0-1 | RAY3D 起始抖动 |
| blurScale | `_BlurScale` | 0-5 | blur 链强度 |
| blurLevels / blurIterations | — | 0-4 | 多级模糊层级 / 迭代 |
| sslMode | 关键字 | — | RAY3D_Fog / RAY3D_Light / RBR2D |
| SSLFeature | — | bool | Debug：显示中间结果 |

RBR2D 内部常量：`decay=0.97`（权重衰减）、`density=0.5`（采样密度/扩散范围）、`weight=0.1`、`SAMPLES=24`。

## RBR2D 关键实现

```hlsl
// 平行光方向 → 屏幕空间焦点 UV
float2 GetLightFocusUV()
{
    Light mainLight = GetMainLight();
    float3 lightPosFar = GetCameraPositionWS() + mainLight.direction * _MaxDistance;
    float4 clip = mul(UNITY_MATRIX_VP, float4(lightPosFar, 1.0));
    if (clip.w <= 0.0) return float2(0.5, 0.5);   // 光在相机背后 → 回退屏幕中心
    float2 focus = clip.xy / clip.w * 0.5 + 0.5;
    focus.y = 1.0 - focus.y;
    return clamp(focus, -0.5, 1.5);               // 允许离屏焦点，限制极端值
}
```

- 焦点 = 平行光方向上的远点经 `UNITY_MATRIX_VP` 投影到屏幕 UV（平行光任意距离投影一致，`_MaxDistance` 仅作数值尺度）
- blur 方向向量 `tuv = uv - focusUV`，采样累积递减权重，焦点处聚光收尾 `col *= 1 - dot(tuv,tuv)*0.75`
- 与样本差异：移除 `lOff()` 假光旋转 hack（用真实主平行光方向），`iTime` → `_Time.y`，`texture(iChannel0)` → `_BlitTexture`

---

## Feature 接入 Volume 的方法（2026-08-27 移除记录，恢复指南）

> 本效果在 2026-08-27 前通过 URP Volume 驱动（VolumeComponent + Volume Profile），后回归纯后处理。
> **通用接入方法**（VolumeComponent 定义、Feature 每帧读取 Volume 栈、关键字切换、Volume vs settings 差异）
> → [volume-component.md](agents/unity-developer/references/unity6-api/volume-component.md)「Feature 接入 Volume 完整流程」。

SSL 特有恢复步骤：

1. `git checkout -- Assets/Mine/Shaders/PostProcess/SSL/SSLVolume.cs*`（或从 `.backup_v2_pre_decouple/` 移回，含 .meta，guid 保留）
2. `SSLFeature.RecordRenderGraph` 开头恢复 Volume 读取块，`settings.xxx` 参数搬运替换为 `vol.xxx.value`
3. 关键字切换按 Volume 版（`SSL_FOG`/`SSL_LIGHT` 由 `vol.sslType` 驱动；`SSL_RBR2D` 需在 SSLVolume 中补充对应枚举项）
4. `Assets/Settings/DefaultVolumeProfile.asset` 添加回 SSLVolume 组件块（可从 `git diff` 或 `/tmp/DefaultVolumeProfile.asset.bak_ssl_20260827` 恢复）

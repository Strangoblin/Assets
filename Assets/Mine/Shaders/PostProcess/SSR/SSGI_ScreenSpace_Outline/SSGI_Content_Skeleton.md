# SSGI 框架实施计划

> 基于已解偶的 SSR 架构（RaySampleFunction.hlsl + RayMarchFunction.hlsl），
> 逐步扩展为完整屏幕空间全局光照框架。

---

## 总体架构

```
                            ┌─────────────────────┐
                            │   SSR.shader         │
                            │   Uniforms + HitProcess │
                            └─────────┬───────────┘
                                      │
              ┌───────────────────────┼───────────────────────┐
              │                       │                       │
    ┌─────────▼─────────┐   ┌────────▼────────┐   ┌─────────▼─────────┐
    │ RaySampleFunction  │   │ RayMarchFunction │   │ SSGI_Filter.hlsl  │
    │ (采样策略)          │   │ (步进策略)        │   │ (时域+空域滤波)    │
    ├───────────────────┤   ├─────────────────┤   ├───────────────────┤
    │ DDA2D → Bin/HiZ   │   │ HitTest (线性)   │   │ TemporalAccum     │
    │ Ray3D → March3D    │   │ BinProcess (二分) │   │ Reproject         │
    │ SSPR (平面镜面)     │   │ HiZProcess (层级) │   │ VarianceClamp     │
    │ StochasticSSR (粗糙)│   │ March3D (世界)    │   │ BilateralBlur     │
    │ DiffuseGI (漫反射)  │   │                  │   │                   │
    │ SSAO (遮挡)         │   │                  │   │                   │
    │ HBAO (方向遮挡)     │   │                  │   │                   │
    └───────────────────┘   └──────────────────┘   └───────────────────┘
```

## 实施路线图

```
Phase 1 ──┐                                      Phase 5 ──┐
SSPR       │   Phase 2       Phase 3    Phase 4    Filter    │  Compute
(简单镜面)  │   StochasticSSR  DiffuseGI  SSAO+HBAO  系统      │  迁移
           │   (粗糙反射)     (漫反射)   (环境光)              │
           │                                                    │
  ─── 1天  │   ─── 2天       ─── 3天    ─── 2天    ─── 2天    │  ─── 3天
           │                                                    │
  └─ 已完成架构基础上增量 ──────────────────────────────────────┘
```

---

## Phase 1: SSPR — 屏幕空间平面镜面反射（零步进翻转版）

### 算法原理

利用"镜像相机"论据直接翻转 UV 采样：无步进、无深度比较。

**核心数学：**
```
水平相机（无俯仰/翻滚）下，平面反射 = 镜像相机成像 = 原画面垂直反转：
    uv' = (u, 1 - v)                    // 精确（任意内容距离，非近似）
相机带俯仰/翻滚时，反转退化为视空间镜像同态变换：
    Rv = reflect(Vv, Nv)                // 视空间镜像方向
    uv' = project(Rv)                   // 方向投影回屏幕
```

**为什么镜像"方向"而非"位置"：**
`P' = mirror(P)` 在单深度缓冲下退化 —— 平面像素的镜像点就是它自己
（uv' = uv → 自采样），反射不可见。镜像视线方向绕开该退化点：
反射方向只由平面法线决定，内容在无穷远假设下不需要任何深度信息。

### 性能优势

- **零步进、零深度比较** — 每像素 1 次场景采样 + 1 次矩阵运算
- 复杂度 O(1)，对比旧步进版 O(n)
- 适合移动端（<0.3ms @ 半分辨率）

### 实施步骤

1. `Frag_SSPR_Trace()` 重写（SSPR.shader Pass 0）:
   ```hlsl
   // 1. 平面检测：法线 buffer 判定水平/竖直面 (planarity > 0.95)
   // 2. 视空间镜像方向 Rv = reflect(Vv, Nv)
   // 3. 方向投影回屏幕 uv'（clip.w ≤ 0 → 射向相机后方 → SH 兜底）
   // 4. 采样 SampleSceneColor(uv')，alpha = 平面度 * smoothness * 远景淡入
   ```
2. 远景淡入：`alpha *= smoothstep(_FlipFade, _MaxDistance, viewDist)`
3. 屏幕边缘过渡：`inBound` 在 0.9→1.0 平滑衰减，内容 ↔ `SampleSH` 天空兜底 lerp
   （alpha 不跳变，无越界硬边界）
4. C#：删除 March 设置，新增 `flipFade`（maxDistance 的倍数）

### 已知问题与处理

| 问题 | 方案 |
|------|------|
| 相机俯仰/翻滚时近处内容位移 | 内容无穷远（远景）时精确；近处由 SSR 步进负责 |
| 屏幕外反射（uv' 越界 / w ≤ 0） | 边缘平滑过渡到 `SampleSH` 天空兜底，无硬边界 |
| 竖平面（墙面镜） | 同态变换自动处理（水平翻转） |
| 与 SSR 步进结果重叠 | `flipFade` 淡入区间分隔（近处 SSR、远处 SSPR） |

---

## Phase 2: Stochastic SSR — 粗糙表面镜面反射

### 算法原理

基于 Frostbite (SIGGRAPH 2015) 方法：每像素 1 条 GGX 重要性采样射线 + 空间重用以降噪。

**两阶段管线：**
```
Stage 1 — Ray March (1 SPP)         Stage 2 — Resolve (空间重用)
┌────────────────────────┐          ┌──────────────────────────┐
│ GGX 重要性采样          │          │ 搜索邻域命中结果           │
│ Hi-Z 加速步进           │    →     │ BRDF 加权平均              │
│ 写入 Hit Buffer (UV+depth)│        │ Cone-tracing MIP 选择     │
│ 记录 PDF                │          │ Prefiltered Color 采样    │
└────────────────────────┘          └──────────────────────────┘
```

### 实施步骤

1. 添加 `StochasticSSR/Settings`（raysPerPixel, GGX samples）
2. 在 RaySampleFunction 中添加 `Frag_SSR_Stochastic()`:
   ```hlsl
   // 1. GGX VNDF 重要性采样（Intel 快速方法，无需构建 TBN）
   float3 H = SampleGGX_VNDF(rand, roughness);
   float3 reflectDir = reflect(-viewDir, H);
   // 2. HiZ 步进（复用已有 RayMarchFunction）
   // 3. 写入 Hit Buffer（UV + depth + PDF）
   ```
3. 添加 `Frag_SSR_Resolve()`:
   ```hlsl
   // 1. 空间搜索（5×5 邻域）
   // 2. 当前像素 BRDF 重加权邻域命中
   // 3. Cone-tracing: mip = log2(hitDist * coneTan * screenSize)
   // 4. 预过滤颜色采样 + FG-term 归一化
   ```

### GGX VNDF 采样核心

```hlsl
// Intel VNDF: 无需 TBN 构建，~15% 更快
float3 SampleGGX_VNDF(float2 xi, float alpha) {
    float phi = 2.0 * PI * xi.x;
    float cosTheta = sqrt((1.0 - xi.y) / (1.0 + (alpha*alpha - 1.0) * xi.y));
    float sinTheta = sqrt(1.0 - cosTheta * cosTheta);
    // 各向同性：半球均匀方位角
    return float3(sinTheta * cos(phi), sinTheta * sin(phi), cosTheta);
}
```

### 依赖

- RayMarchFunction 已有 HiZProcess → 直接复用
- 需要新增：Hit Buffer RT + Resolve Pass

---

## Phase 3: Diffuse GI — 屏幕空间间接漫反射

### 算法原理

半球 cosine-weighted 随机采样 + 屏幕空间步进 + 降噪。

**核心公式：**
```
Lo = ∫_Ω (ρ/π) * Li * cos(θ) dω
   ≈ (ρ/π) * Σ(π * Li_sample) / N          (cos/PDF 抵消)
```

### 关键优化：Cache-Aware 半球采样

```
朴素方法：每像素独立旋转采样方向 → 相邻像素射线方向散乱 → L2 缓存频繁失效

优化方法（Intersil Patent）：
1. 预计算单组 Poisson Disk 采样（Morton 码排序）
2. 每像素做小扰动 jitter → 同 warp 内射线方向接近
3. 屏幕空间缓存命中率提升 → 实测最高 62% 加速
```

### 实施步骤

1. 添加 `Frag_SSGI_Diffuse()`:
   ```hlsl
   // 1. TBN 构建
   // 2. Cosine-weighted 半球采样（1-2 rays/pixel）
   // 3. 屏幕空间步进（March3D 或 DDA，按 w 安全度选择）
   // 4. 命中选择（取 first-hit 或 weighted blend）
   // 5. 写入 DiffuseRT
   ```
2. 添加空间降噪 Pass:
   ```hlsl
   // 1/4 分辨率 bilateral blur (深度 + 法线引导)
   // 逐级 upsample back to full res
   ```
3. 重构 HitProcess → 添加距离衰减：
   ```hlsl
   float distAtten = 1.0 / (1.0 + hitDist * hitDist * _DiffuseFalloff);
   alpha *= distAtten;
   ```

### 依赖

- March3D/HiZProcess 复用
- 需要新增 DiffuseRT + 降噪 Pass

---

## Phase 4: SSAO + HBAO — 环境光遮蔽

### SSAO（基础）

**原理：** 在屏幕空间半球内随机采样深度，统计被遮挡比例。

```hlsl
float occlusion = 0;
for (int i = 0; i < kernelSize; i++) {
    float3 samplePos = positionVS + kernel[i] * radius;
    float4 clip = mul(Projection, float4(samplePos, 1));
    float2 sampleUV = (clip.xy / clip.w) * 0.5 + 0.5;
    float sampleDepth = LinearEyeDepth(SampleSceneDepth(sampleUV));
    occlusion += (samplePos.z > sampleDepth) ? 1.0 : 0.0;  // 被遮挡
}
ao = 1.0 - occlusion / kernelSize;
```

**优化：** 1/2 分辨率 + depth-only bilateral blur

### HBAO（方向感知）

**算法步骤（NVIDIA 2008）：**

```
1. 深度线性化 + 视空间位置重建

2. 方向切片：
   N 个等角方向（通常 4-8），每方向独立步进
   16 层 de-interleaved 纹理（同方向像素分一组 → 缓存友好）

3. 地平线角度追踪：
   每步计算仰角 tan(h) = (S_z - P_z) / dist
   追踪最大地平线角度

4. 遮挡积分：
   AO += falloff(dist²) * (sin(h_max) - sin(t))
   t = 切平面倾角（由法线确定）

5. Cross-bilateral blur
```

**关键细节：** 切线角度 t 防止曲面上自遮挡；角度偏差 30° 应对低纹理曲面。

### 实施步骤

1. 添加 `Frag_SSAO()` → 最简单原型验证
2. 添加 `Frag_HBAO()` → 方向切片 + 角度追踪
3. 两种模式统一输出到 `_AOTexture`
4. 更新 Composite 公式：`Final = Direct + Diffuse*AO + Specular*AO`

### 依赖

- 无特殊依赖，仅需深度 + 法线
- HBAO 需要 16 层 de-interleaved RT

---

## Phase 5: 时域 + 空域滤波系统

### 统一滤波管线

```
  采样结果 RT（RGB + 置信度 A）
        │
        ▼
  ┌─────────────────┐
  │ TemporalAccum    │  ← MotionVector + 历史帧重投影
  │ (时域累积)       │
  └────────┬────────┘
           ▼
  ┌─────────────────┐
  │ VarianceClamp    │  ← 压灭萤火虫（clamp to local mean ± variance）
  │ (方差钳制)       │
  └────────┬────────┘
           ▼
  ┌─────────────────┐
  │ BilateralBlur    │  ← 深度 + 法线引导的双边模糊
  │ (空域降噪)       │     1/4 → 1/2 → Full resolution
  └────────┬────────┘
           ▼
      输出 RT
```

### 实施步骤

1. 创建 `SSGI_Filter.hlsl`
2. 添加 `Frag_TemporalAccum()` — 历史帧混合
3. 添加 `Frag_VarianceClamp()` — 邻域统计钳制
4. 添加 `Frag_BilateralBlur()` — 可复用现有 BlurFunction

### 输入约定

```
采样层输出格式（所有算法统一）：
  RT RGBA:  RGB = Lighting Result,  A = Confidence (0-1)

滤波器消费格式：
  输入: 上述 RT + MotionVector + History RT
  输出: 滤波后的 RT（同格式）
```

---

## Composite 完整公式

```
FinalColor = DirectLighting
           + IndirectDiffuse  * DiffuseBRDF  * AO  * distAtten
           + IndirectSpecular * Fresnel       * AO  * roughness
           + Ambient          * (1 - AO)
```

其中：
- `IndirectDiffuse` 来自 DiffuseGI Phase
- `IndirectSpecular` 由 SSR/SSPR/StochasticSSR 按 roughness 混合：
  - roughness < 0.1 → SSPR（镜面）或 SSR_DDA（非平面）
  - roughness 0.1-0.6 → Stochastic SSR（1 ray + 空间重用）
  - roughness > 0.6 → 跳过（cosine 锥角太大，等价于 diffuse）
- `AO` 优先级：HBAO > SSAO（按 Quality 设置选择）

---

## 文件结构

```
Assets/Mine/Shaders/PostProcess/SSR/
├── SSR.shader                     ← 主 Shader（Pass 编排，现阶段）
├── SSRFeature.cs                  ← C# RecordRenderGraph
├── RayMarchFunction.hlsl          ← 步进策略（已解偶，无需修改）
├── RaySampleFunction.hlsl         ← 采样策略（逐步添加新函数）
├── SSGI_Filter.hlsl               ← 时域+空域滤波（Phase 5 新增）
├── SSGI_Common.hlsl               ← 共享工具函数（GGX采样、半球采样等）
└── SSGI_ScreenSpace_Outline/
    └── SSGI_Content_Skeleton.md   ← 本文件
```

---

## 步进策略选择矩阵

| 采样算法 | 推荐步进 | 备选步进 | 选择条件 |
|---------|---------|---------|---------|
| SSPR | 无步进（直接投影） | — | 平面反射约束 |
| SSR (镜面) | HiZProcess | BinProcess | 默认 DDA；w 危险时切换 March3D |
| SSR (粗糙) | HiZProcess（锥角增大 mip） | March3D | roughness < 0.6 |
| Diffuse GI | March3D | HiZProcess | 半球方向多样，世界步进更鲁棒 |
| SSAO | HitTest（短距离） | — | 半径小（1-2 世界单位） |
| HBAO | 角度切片步进 | — | 4-8 方向独立步进 |

---

## 与现有 SSR 代码复用的接口

### HitProcess 扩展

```hlsl
// 现有
float4 HitProcess(float4 color, float3 reflectDir, float2 hitUV)
// → alpha *= _Smoothness * edgeFade

// 扩展后
float4 HitProcess(float4 color, float3 reflectDir, float2 hitUV, float hitDist)
// → alpha *= _Smoothness * edgeFade * DistanceAtten(hitDist)
```

向后兼容：新增 `hitDist` 参数默认值 0（SSR 现有调用不受影响）。

### 混合步进（Phase 1 实施）

```hlsl
// Frag_SSR_DDA2D 中添加 w 安全检查
float wSafe = endCS.w / startCS.w;
if (wSafe < 0.3) {
    // 回退到 March3D 世界空间步进
    color = March3D(color, reflectDir, dw, jitteredStartW);
} else {
    // DDA 正常路径
    color = BinProcess(color, reflectDir, dk, ds, dv, K, S, V);
}
```

---

## 参考来源

- **Stochastic SSR**: [SIGGRAPH 2015 Frostbite](http://advances.realtimerendering.com/s2015/Stochastic%20Screen-Space%20Reflections.pptx) | [Intel VNDF Sampling](https://community.intel.com/t5/Blogs/Tech-Innovation/Artificial-Intelligence-AI/VNDF-importance-sampling-for-an-isotropic-Smith-GGX-distribution/post/1599836)
- **SSPR**: [GDC 2017 Ghost Recon Wildlands](https://zhuanlan.zhihu.com/p/651134124) | Blender EEVEE `effect_ssr_frag.glsl`
- **Diffuse GI**: [Cache-Aware Hemisphere Sampling](https://patentimages.storage.googleapis.com/57/9b/d6/f5eb79099d2046/US20180040155A1.pdf)
- **HBAO**: [NVIDIA SIGGRAPH 2008](http://developer.download.nvidia.com/presentations/2008/SIGGRAPH/HBAO_SIG08b.pdf) | [De-interleaved Texturing](https://github.com/study-game-engines/nvidia-ssao-demo)
- **HiZ DDA**: [Morgan McGuire - Efficient GPU Screen-Space Ray Tracing](https://research.nvidia.com/publication/efficient-gpu-screen-space-ray-tracing)

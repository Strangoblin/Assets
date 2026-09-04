// ════════════════════════════════════════════════════════════════
//  RaySampleFunction.hlsl — 屏幕空间采样策略
// ════════════════════════════════════════════════════════════════
//
//  职责：从像素出发，生成反射射线，设置齐次/世界空间步进参数，
//        调度 RayMarchFunction 中的步进策略完成光线追踪。
//
//  包含采样策略：
//    Frag_SSR_DDA2D() — DDA 2D：齐次坐标线性插值 → BinProcess / HiZProcess
//    Frag_SSR_RAY3D() — Ray3D：世界空间等距步进 → March3D
//
//  期望外部提供：
//    Uniforms:  _MaxDistance, _StepSize, _JitterScale, _StepCount,
//               _CameraViewMatrix, _CameraProjectionMatrix
//    Functions: HitProcess(float4, float3, float2) → float4
//               BinProcess / HiZProcess / March3D (来自 RayMarchFunction.hlsl)
//               SampleSceneDepth, ComputeWorldSpacePosition,
//               GetCameraPositionWS, SampleSceneNormals
//
//  接口约定（SSGI 骨架 — 采样层）：
//    输入: 屏幕像素 (uv)
//    输出: float4 (RGB=反射颜色, A=置信度)
// ════════════════════════════════════════════════════════════════

#ifndef RAYSAMPLEFUNCTION_HLSL_INCLUDED
#define RAYSAMPLEFUNCTION_HLSL_INCLUDED

// ════════════════════════════════════════════════════════════════
//  Frag_SSR_DDA — DDA 2D 采样
//
//  流程：
//    1. 重建世界位置、法线、反射方向
//    2. 将反射射线端点投影到齐次坐标 (K=1/w, S=UV, V=viewPos/w)
//    3. 计算齐次空间步进增量 (dk, ds, dv)
//    4. 调度 BinaryProcess 或 HiZProcess
//
//  注意：当 endCS.w → 0 时 K → ∞，导致鱼眼扭曲。
//        详见 memory/2026-08-07-ssr-dda-fisheye-w-sign-flip.md
// ════════════════════════════════════════════════════════════════

half4 Frag_SSR_DDA2D(Varyings input) : SV_Target
{
    // ── 初始设置 ──
    float4 color = half4(0, 0, 0, 0);
    float2 uv = input.texcoord;
    float rawDepth = SampleSceneDepth(uv);

    float3 positionWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
    float3 viewDir    = normalize(positionWS - GetCameraPositionWS());
    float3 normalWS   = SampleSceneNormals(uv);
    float3 reflectDir = reflect(viewDir, normalWS);

    float ndotv = saturate(dot(normalWS, -viewDir));
    if (ndotv <= 0.0)
        return color;

    // ── DDA Ray 设置：齐次坐标端点投影 ──
    float3 startWS = positionWS;
    float3 endWS   = positionWS + reflectDir * _MaxDistance;
    float3 startVS = mul(_CameraViewMatrix, float4(startWS, 1)).xyz;
    float3 endVS   = mul(_CameraViewMatrix, float4(endWS,   1)).xyz;
    float4 startCS = mul(_CameraProjectionMatrix, float4(startVS, 1));
    float4 endCS   = mul(_CameraProjectionMatrix, float4(endVS,   1));

    // 齐次坐标计算
    float  startK = 1.0 / startCS.w;
    float  endK   = 1.0 / endCS.w;
    float2 startS = (float2(startCS.x, startCS.y * _ProjectionParams.x) * startK) * 0.5 + 0.5;
    float2 endS   = (float2(endCS.x,   endCS.y   * _ProjectionParams.x) * endK)   * 0.5 + 0.5;
    float3 startV = startVS * startK;
    float3 endV   = endVS   * endK;

    // 步进增量
    float  dk = (endK - startK) / _StepCount * _StepSize;
    float2 ds = (endS - startS) / _StepCount * _StepSize;
    float3 dv = (endV - startV) / _StepCount * _StepSize;

    // Jitter
    float jitter = frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
    float  K = startK + jitter * _JitterScale * dk;
    float2 S = startS + jitter * _JitterScale * ds;
    float3 V = startV + jitter * _JitterScale * dv;

    // ── 调度步进策略 ──
    #if defined(SSR_HIZ2D)
        color = HiZProcess(color, reflectDir, dk, ds, dv, K, S, V);
    #else
        color = BinProcess(color, reflectDir, dk, ds, dv, K, S, V);
    #endif
    return color;
}

// ════════════════════════════════════════════════════════════════
//  Frag_SSR_Ray3D — 世界空间 3D 采样
//
//  流程：
//    1. 重建世界位置、法线、反射方向
//    2. 在 world space 等距步进
//    3. 每步独立透视投影到屏幕空间
//    4. 屏幕 UV 出界或深度命中时退出
//
//  优势：天然处理近平面 w 过零（clip.w 变号 → S 自动出界）
//  代价：每步一次 4×4 矩阵乘法
// ════════════════════════════════════════════════════════════════

half4 Frag_SSR_RAY3D(Varyings input) : SV_Target
{
    // ── 初始设置 ──
    float4 color = half4(0, 0, 0, 0);
    float2 uv = input.texcoord;
    float rawDepth = SampleSceneDepth(uv);

    float3 positionWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
    float3 viewDir    = normalize(positionWS - GetCameraPositionWS());
    float3 normalWS   = SampleSceneNormals(uv);
    float3 reflectDir = reflect(viewDir, normalWS);

    float ndotv = saturate(dot(normalWS, -viewDir));
    if (ndotv <= 0.0)
        return color;

    // ── 世界空间步进设置 ──
    float3 startWS = positionWS;
    float3 endWS   = positionWS + reflectDir * _MaxDistance;
    float3 dw = (endWS - startWS) / _StepCount * _StepSize;

    float jitter = frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
    float3 W = startWS + jitter * _JitterScale * dw;

    color = RayProcess(color, reflectDir, dw, W);
    return color;
}

#endif // RAYSAMPLEFUNCTION_HLSL_INCLUDED

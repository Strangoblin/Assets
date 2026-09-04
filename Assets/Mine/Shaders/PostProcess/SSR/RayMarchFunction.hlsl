// ════════════════════════════════════════════════════════════════
//  RayMarchFunction.hlsl — 屏幕空间步进策略
// ════════════════════════════════════════════════════════════════
//
//  职责：在已知射线方向（dk, ds, dv）和起点（K, S, V）的条件下，
//        沿着屏幕空间步进，检测深度命中。
//
//  包含 4 种步进策略：
//    HitTest()   — 线性步进 + 深度比较（底层原语）
//    BinProcess() — 二分精炼（精确命中）
//    HiZProcess() — Hi-Z 层级自适应步进（加速）
//    March3D()   — 世界空间等距步进 + 每步透视投影（鲁棒）
//
//  接口约定（SSGI 骨架 — 步进层）：
//    DDA 系输入:  (dk, ds, dv, K, S, V) → HitTest / BinProcess / HiZProcess
//    WS  系输入:  (dw, startW)           → March3D
// ════════════════════════════════════════════════════════════════

#ifndef RAYMARCHFUNCTION_HLSL_INCLUDED
#define RAYMARCHFUNCTION_HLSL_INCLUDED

// ── HiZ 深度金字塔纹理 ──
TEXTURE2D_X(_HiZTex0);
TEXTURE2D_X(_HiZTex1);
TEXTURE2D_X(_HiZTex2);
TEXTURE2D_X(_HiZTex3);
TEXTURE2D_X(_HiZTex4);
TEXTURE2D_X(_HiZTex5);
TEXTURE2D_X(_HiZTex6);
TEXTURE2D_X(_HiZTex7);

// ════════════════════════════════════════════════════════════════
//  HiZ 深度金字塔采样
// ════════════════════════════════════════════════════════════════

float SampleHiZDepthAtMip(float2 uv, int mipLevel)
{
    [flatten]
    switch (mipLevel)
    {
        case 0: return SAMPLE_TEXTURE2D_X(_HiZTex0, sampler_LinearClamp, uv).r;
        case 1: return SAMPLE_TEXTURE2D_X(_HiZTex1, sampler_LinearClamp, uv).r;
        case 2: return SAMPLE_TEXTURE2D_X(_HiZTex2, sampler_LinearClamp, uv).r;
        case 3: return SAMPLE_TEXTURE2D_X(_HiZTex3, sampler_LinearClamp, uv).r;
        case 4: return SAMPLE_TEXTURE2D_X(_HiZTex4, sampler_LinearClamp, uv).r;
        case 5: return SAMPLE_TEXTURE2D_X(_HiZTex5, sampler_LinearClamp, uv).r;
        case 6: return SAMPLE_TEXTURE2D_X(_HiZTex6, sampler_LinearClamp, uv).r;
        default: return SAMPLE_TEXTURE2D_X(_HiZTex7, sampler_LinearClamp, uv).r;
    }
}

half4 SampleHiZDepth(float2 uv, int mipLevel)
{
    return SampleHiZDepthAtMip(uv, mipLevel);
}

half4 SampleDepth(float2 uv, float2 offset, int mipLevel)
{
    offset *= _TexelSize.xy;
    return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + offset, mipLevel);
}

// ════════════════════════════════════════════════════════════════
//  HitTest — 线性步进 + 深度比较
//
//  沿齐次坐标方向 (dk, ds, dv) 线性步进 _StepCount 次，
//  每次比较 rayEyeDepth 与 sceneEyeDepth，检测射线是否穿过表面。
// ════════════════════════════════════════════════════════════════

bool HitTest(float dk, float2 ds, float3 dv, inout float K, inout float2 S, inout float3 V, out float depthDiff, out float thickness)
{
    [loop]
    for (int i = 0; i < _StepCount; i++)
    {
        K += dk;
        S += ds;
        V += dv;

        if (S.x < 0 || S.x > 1 || S.y < 0 || S.y > 1)
            return false;

        float sceneRawDepth = SampleSceneDepth(S);
        float sceneEyeDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
        float rayEyeDepth   = -V.z / K;
        depthDiff = rayEyeDepth - sceneEyeDepth;
        thickness = _Thickness;
        if (depthDiff > 0)
        {
            return true;
        }
    }
    return false;
}

// ════════════════════════════════════════════════════════════════
//  BinProcess — 二分精炼
//
//  HitTest 检测到穿透后，回退一步并逐次减半步长，
//  在 _BinCount 次迭代内收敛到精确命中点。
// ════════════════════════════════════════════════════════════════

half4 BinProcess(float4 color, float3 reflectDir, float dk, float2 ds, float3 dv, float K, float2 S, float3 V)
{
    [loop]
    for (int i = 0; i < _BinCount; i++)
    {
        float depthDiff;
        float thickness;
        bool hit = HitTest(dk, ds, dv, K, S, V, depthDiff, thickness);
        if (hit)
        {
            if (depthDiff < thickness)
            {
                float4 result = HitProcess(color, reflectDir, S);
                if (result.a > 0.0)
                {
                    color = half4(result.rgb, result.a);
                    break;
                }
            }
            K -= dk;
            S -= ds;
            V -= dv;

            dk *= 0.5;
            ds *= 0.5;
            dv *= 0.5;
        }
        else
        {
            break;
        }
    }
    return color;
}

// ════════════════════════════════════════════════════════════════
//  HiZProcess — Hi-Z 层级自适应步进
//
//  从 mip 0 开始，根据深度比较结果动态升降 mip level：
//    depthDiff < 0（射线在场景前方）→ mip++（加速跨越空白）
//    depthDiff > 0（射线在场景后方）→ mip--（精细搜索）
//  mip 0 命中时调用 HitProcess 验证。
// ════════════════════════════════════════════════════════════════

half4 HiZProcess(float4 color, float3 reflectDir, float dk, float2 ds, float3 dv, float K, float2 S, float3 V)
{
    int mipLevel = 0;
    [loop]
    for (int i = 0; i < _StepCount; i++)
    {
        K += dk * exp2(mipLevel);
        S += ds * exp2(mipLevel);
        V += dv * exp2(mipLevel);

        if (S.x < 0 || S.x > 1 || S.y < 0 || S.y > 1)
            break;

        float sceneRawDepth = SampleHiZDepth(S, mipLevel).r;
        float sceneEyeDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
        float rayEyeDepth   = -V.z / K;
        float depthDiff = rayEyeDepth - sceneEyeDepth;

        if (depthDiff < 0)
        {
            mipLevel = min(mipLevel + 1, _MaxMipLevel);
        }
        else
        {
            if (mipLevel == 0)
            {
                if (depthDiff < _Thickness)
                {
                    float4 result = HitProcess(color, reflectDir, S);
                    if (result.a > 0.0)
                    {
                        color = half4(result.rgb, result.a);
                        break;
                    }
                }
            }
            else
            {
                K -= dk * exp2(mipLevel);
                S -= ds * exp2(mipLevel);
                V -= dv * exp2(mipLevel);

                mipLevel--;
            }
        }
    }
    return color;
}

// ════════════════════════════════════════════════════════════════
//  RayProcess — 世界空间等距步进
//
//  在世界空间中沿反射方向等距步进 _StepCount 次，
//  每步独立做透视投影到屏幕空间进行深度比较。
//
//  输入: color       — 当前累积颜色（初始为 0）
//        reflectDir  — 世界空间反射方向
//        dw          — 世界空间步进向量（已含 _StepSize 缩放）
//        startW      — 世界空间起点（已含 jitter）
//
//  优势：天然处理近平面 w 过零（clip.w 变号 → S 自动出界）
//  代价：每步一次 4×4 矩阵乘法
// ════════════════════════════════════════════════════════════════

half4 RayProcess(float4 color, float3 reflectDir, float3 dw, float3 startW)
{
    float3 W = startW;
    [loop]
    for (int i = 0; i < _StepCount; i++)
    {
        W += dw;

        float4 clip = mul(_CameraProjectionMatrix, mul(_CameraViewMatrix, float4(W, 1)));
        float2 S = (float2(clip.x, clip.y * _ProjectionParams.x) * rcp(clip.w)) * 0.5 + 0.5;

        if (S.x < 0 || S.x > 1 || S.y < 0 || S.y > 1)
            break;

        float sceneRawDepth = SampleSceneDepth(S);
        float sceneEyeDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
        float rayEyeDepth   = clip.w;
        float depthDiff = rayEyeDepth - sceneEyeDepth;

        if (depthDiff > 0 && depthDiff < _Thickness)
        {
            float4 result = HitProcess(color, reflectDir, S);
            if (result.a > 0.0)
            {
                color = half4(result.rgb, result.a);
                break;
            }
        }
    }
    return color;
}

// ════════════════════════════════════════════════════════════════
//  Frag_HiZDepthMip — HiZ 深度金字塔降采样 Pass
//
//  对上一级 mip 的 4 个相邻像素取 max，生成当前 mip。
//  由 C# SSRFeature 在 Pass 3 (SSR_HiZDepthMip) 中调用。
// ════════════════════════════════════════════════════════════════

half4 Frag_HiZDepthMip(Varyings input) : SV_Target
{
    float2 uv = input.texcoord;
    half4 depth = half4(
        SampleDepth(uv, float2(-1, -1), _FromMipLevel).r,
        SampleDepth(uv, float2( 1, -1), _FromMipLevel).r,
        SampleDepth(uv, float2(-1,  1), _FromMipLevel).r,
        SampleDepth(uv, float2( 1,  1), _FromMipLevel).r
    );
    return max(max(depth.x, depth.y), max(depth.z, depth.w));
}

#endif // RAYMARCHFUNCTION_HLSL_INCLUDED

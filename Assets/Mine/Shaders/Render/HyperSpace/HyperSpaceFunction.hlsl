// ════════════════════════════════════════════════════════════
//  HyperSpaceFunction.hlsl — Shadertoy 原始核心函数
//  Benoit Marini, 2020 / CC BY-NC-SA 3.0
// ════════════════════════════════════════════════════════════

#ifndef HYPERSPACE_FUNCTION_HLSL
#define HYPERSPACE_FUNCTION_HLSL

#define HYPERSPACE_ITER 23

// 原文：
//   vec4 o = vec4(p.xyz, 3.*sin(t*.1));
//   vec4 dec = vec4(1.,.9,.1,.15)
//          + vec4(.06*cos(t*.1),0,0,.14*cos(t*.23));
//   for (int i=0 ; i++ < ITER;) o.xzyw = abs(o/dot(o,o)-dec);
float4 HyperSpaceTex(float3 p, float time)
{
    float4 o = float4(p, 3.0 * sin(time * 0.1));
    float4 dec = float4(1.0, 0.9, 0.1, 0.15)
               + float4(0.06 * cos(time * 0.1), 0.0, 0.0, 0.14 * cos(time * 0.23));

    [unroll]
    for (int iteration = 0; iteration < HYPERSPACE_ITER; iteration++)
    {
        float4 folded = abs(o / dot(o, o) - dec);
        o = folded.xzyw;
    }

    return o;
}

#endif // HYPERSPACE_FUNCTION_HLSL

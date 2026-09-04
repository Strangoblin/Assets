// ════════════════════════════════════════════════════════════
//  HyperTubeFunction.hlsl — Shadertoy 核心函数
//  Shader by Frostbyte / CC BY-NC-SA 4.0
// ════════════════════════════════════════════════════════════

#ifndef HYPERTUBE_FUNCTION_HLSL
#define HYPERTUBE_FUNCTION_HLSL

#define HYPERTUBE_RAYMARCH_STEPS 10
#define HYPERTUBE_PHI 1.618033988

// ════════════════════════════════════════════════════════════
//  二维旋转、ACES 色调映射与 Dot Noise
// ════════════════════════════════════════════════════════════
float2 HyperTubeRotate(float2 value, float angle)
{
    float sine = sin(angle);
    float cosine = cos(angle);
    return mul(value, float2x2(cosine, -sine, sine, cosine));
}

float3 HyperTubeAces(float3 color)
{
    const float3x3 m1 = float3x3(
        0.59719, 0.07600, 0.02840,
        0.35458, 0.90834, 0.13383,
        0.04823, 0.01566, 0.83777);
    const float3x3 m2 = float3x3(
         1.60475, -0.10208, -0.00327,
        -0.53108,  1.10813, -0.07276,
        -0.07367, -0.00605,  1.07602);

    float3 transformed = mul(color, m1);
    float3 numerator = transformed * (transformed + 0.0245786) - 0.000090537;
    float3 denominator = transformed * (0.983729 * transformed + 0.4329510) + 0.238081;
    return mul(numerator / denominator, m2);
}

float HyperTubeDotNoise(float3 position)
{
    const float3x3 gold = float3x3(
        -0.571464913,  0.814921382,  0.096597072,
        -0.278044873, -0.303026659,  0.911518454,
         0.772087367,  0.494042493,  0.399753815);

    float3 goldPosition = mul(position, gold);
    float3 positionGold = mul(gold, position);
    return dot(cos(goldPosition), sin(HYPERTUBE_PHI * positionGold));
}

// ════════════════════════════════════════════════════════════
//  HyperTube — 十步低采样体积光线步进
// ════════════════════════════════════════════════════════════
float3 HyperTubeRender(float2 fragCoord, float2 resolution, float time)
{
    float3 position = float3(0.0, 0.0, time);
    float3 light = 0.0;
    float3 direction = normalize(float3(2.0 * fragCoord - resolution, resolution.y));

    [unroll]
    for (int step = 0; step < HYPERTUBE_RAYMARCH_STEPS; step++)
    {
        float iteration = (float)step;
        float3 samplePosition = position;
        samplePosition.xy = HyperTubeRotate(
            sin(samplePosition.xy),
            time * 1.5 + samplePosition.z * 3.0);

        float distance = 0.001
            + abs(HyperTubeDotNoise(samplePosition * 12.0) / 12.0
                - HyperTubeDotNoise(samplePosition)) * 0.4;
        distance = max(distance, 2.0 - length(position.xy));
        distance += abs(position.y * 0.75
            + sin(position.z + time * 0.1 + position.x * 1.5)) * 0.2;

        position += direction * distance;
        light += (1.0 + sin(
            iteration + length(position.xy * 0.1) + float3(3.0, 1.5, 1.0))) / distance;
    }

    return HyperTubeAces(light * light / 600.0);
}

#endif // HYPERTUBE_FUNCTION_HLSL

// ═══════════════════════════════════════════════════════════════
//  普通 URP 材质 Shader 标准骨架（函数无关 · 可编译）
//
//  结构规范: references/standard/shader/shader-structure.md
//  实源参考: Assets/Mine/Shaders/Render/PBRToon/PBRToon.shader
//
//  使用方式:
//    1. 复制此文件, 改名为 YourShader.shader, 替换 ⚠️ 标记
//    2. 结构铁律: Properties 只做参数暴露; 渲染代码全部在
//       HLSLINCLUDE; SubShader 只定义 Pass（状态 + #pragma）
//    3. 复杂功能拆独立 .hlsl（Assets/Mine/Special/HLSL/）,
//       Shader 内不写算法细节（细节交同名 .md）
// ═══════════════════════════════════════════════════════════════

Shader "Custom/YourShader" // ⚠️ 重命名为你的效果名
{
    // ═══ Properties — 对外参数, [Header] 按功能分组, 与 CBUFFER 一一对应 ═══
    Properties
    {
        [Header(Base)]
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _MainTex ("Main Texture", 2D) = "white" {}
        // ⚠️ [Header(Function)] 新功能组
        // _Roughness ("Roughness", Range(0, 1)) = 0.5

        // ⚠️ 功能开关用 [Toggle(KEYWORD)] — 名字决定 #pragma shader_feature_local
    }

    HLSLINCLUDE
    // ═══ include — 顺序: URP 内置库 → 自有功能库 ═══
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
    // ⚠️ #include "Assets/Mine/Special/HLSL/YourFunction.hlsl"

    // ═══ 纹理声明 — 紧跟 include, TEXTURE2D/SAMPLER 成对 ═══
    TEXTURE2D_X(_MainTex);
    SAMPLER(sampler_MainTex);

    // ═══ CBUFFER — 参数统一管理, 字段名/类型与 Properties 一致 ═══
    CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor;
        // ⚠️ float _Roughness;
    CBUFFER_END

    // ═══ 顶点输入/输出 — 字段按 position → normal → tangent → uv 排列 ═══
    struct YourAttributes // ⚠️ 命名: 前缀式 XxxAttributes
    {
        float4 positionOS : POSITION;
        float3 normalOS   : NORMAL;
        float4 tangentOS  : TANGENT; // ⚠️ 无法线贴图需求可删 tangent/uv
        float2 uv         : TEXCOORD0;
    };

    struct YourVaryings // ⚠️ 命名: 前缀式 XxxVaryings
    {
        float4 positionCS : SV_POSITION;
        float3 normalWS   : TEXCOORD0;
        float2 uv         : TEXCOORD1;
        // ⚠️ float3 positionWS : TEXCOORD2; — 需要世界坐标光照时打开
    };

    // ════════════════════════════════════════════════════════════
    //  Vert — 对象空间 → 裁剪空间, 输出法线/uv 等插值量
    // ════════════════════════════════════════════════════════════
    YourVaryings Vert(YourAttributes input)
    {
        YourVaryings output;
        output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
        output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
        output.uv         = input.uv;
        return output;
    }

    // ════════════════════════════════════════════════════════════
    //  Frag — 主光照, 简易 Lambert; 功能分级扩展见 shader-structure.md
    // ════════════════════════════════════════════════════════════
    half4 Frag(YourVaryings input) : SV_Target
    {
        half4 baseColor = _BaseColor * SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv);

        // ⚠️ 默认只算主光源; 多光源/阴影变体在 Pass 中加 #pragma multi_compile
        Light mainLight = GetMainLight();
        half3 ndl = saturate(dot(input.normalWS, mainLight.direction));
        half3 color = baseColor.rgb * mainLight.color * ndl;

        return half4(color, baseColor.a);
    }

    // ═══ 深度/阴影输出 — URP 深度 Pass 的片元, 由官方模板提供 ═══
    // ⚠️ 透明材质 (ZWrite Off + Blend) 不适用 ShadowCaster, 见 shader-structure.md
    half4 Frag_DepthOnly(YourVaryings input) : SV_Target
    {
        return 0;
    }
    ENDHLSL

    // ═══ SubShader — 仅 Pass 定义（状态 + 编译指令）, 不含函数体 ═══
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        // ═══ 主光照 Pass ═══
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            // ⚠️ 功能开关/阴影多编译变体按需追加, 如:
            // #pragma shader_feature_local ENABLE_YOURFEATURE
            // #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            ENDHLSL
        }

        // ═══ 阴影 Pass — 官方 ShadowCasterPass.hlsl 提供 ═══
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0
            ZWrite On

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            ENDHLSL
        }

        // ═══ 深度 Pass — 深度渲染/遮挡剔除用 ═══
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag_DepthOnly
            ENDHLSL
        }

        // ⚠️ 法线深度 Pass (DepthNormals): 需要深度法线纹理时按下例补
        // Pass
        // {
        //     Name "DepthNormals"
        //     Tags { "LightMode" = "DepthNormals" }
        //     HLSLPROGRAM
        //     #pragma vertex Vert
        //     #pragma fragment Frag_DepthNormals  // ⚠️ 需自定义输出
        //     ENDHLSL
        // }
    }

    // ⚠️ 兜底: 内置管线下材质不可见时提示; URP 通常不需要 FallBack
    // FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

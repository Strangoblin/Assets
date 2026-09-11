// ════════════════════════════════════════════════════════════
//  TRS.hlsl — 2D UV TRS 与 3D 纯数学变换约定
//
//  定位：共享 A 档纯函数库。
//  依赖：零 URP include、零 CBUFFER、零纹理；调用方负责提供参数。
//  用法：#include "Assets/Mine/Special/HLSL/TRS.hlsl"
//  约定：点变换顺序为 T * R * S（先绕 pivot 缩放，再旋转，最后平移）。
//
//  3D 说明：
//  1) TRS3D_* 只处理物体局部/世界空间，不负责相机。
//  2) 需要透视时，可先把点变到 camera space，再调用
//     TRS3D_ProjectCameraPoint。它只需 camera-space 点、焦距和宽高比，
//     不必在每个粒子上显式构造 viewMatrix；这是“模拟 3D”路径。
//  3) 真实路径应使用 URP 的 TransformWorldToHClip / UNITY_MATRIX_VP，
//     或由深度重建 world position 后再做 TRS。共享库不持有 view/projection
//     全局，避免把相机策略固化在这里。
//  4) 后处理中的单层雪花没有可靠的真实 3D 坐标；只有屏幕 UV 和可选深度。
//     因此默认使用 camera-space 深度层级的模拟路径。若要求遮挡、视差和
//     真实空间运动，必须额外重建 world position，并接受深度纹理与逆矩阵成本。
// ════════════════════════════════════════════════════════════

#ifndef TRS_HLSL_INCLUDED
#define TRS_HLSL_INCLUDED

float2 TRS2D_Rotate(float2 point, float radiansAngle)
{
    float sine;
    float cosine;
    sincos(radiansAngle, sine, cosine);
    return float2(cosine * point.x - sine * point.y,
                  sine * point.x + cosine * point.y);
}

float2 TRS2D_TransformPoint(float2 uv, float2 translation, float radiansAngle,
                            float2 scale, float2 pivot)
{
    float2 local = (uv - pivot) * scale;
    return TRS2D_Rotate(local, radiansAngle) + pivot + translation;
}

float2 TRS2D_InverseTransformPoint(float2 uv, float2 translation, float radiansAngle,
                                   float2 scale, float2 pivot)
{
    float2 local = TRS2D_Rotate(uv - pivot - translation, -radiansAngle);
    return local / max(abs(scale), float2(1e-6, 1e-6)) * sign(scale) + pivot;
}

float2 TRS2D_TransformDirection(float2 direction, float radiansAngle, float2 scale)
{
    return TRS2D_Rotate(direction * scale, radiansAngle);
}

float4 TRS3D_NormalizeQuaternion(float4 quaternion)
{
    return quaternion / max(length(quaternion), 1e-6);
}

float3 TRS3D_Rotate(float3 point, float4 quaternion)
{
    float4 q = TRS3D_NormalizeQuaternion(quaternion);
    float3 t = 2.0 * cross(q.xyz, point);
    return point + q.w * t + cross(q.xyz, t);
}

float3 TRS3D_InverseRotate(float3 point, float4 quaternion)
{
    float4 q = TRS3D_NormalizeQuaternion(quaternion);
    return TRS3D_Rotate(point, float4(-q.xyz, q.w));
}

float3 TRS3D_TransformPoint(float3 position, float3 translation, float4 rotation,
                            float3 scale, float3 pivot)
{
    float3 local = (position - pivot) * scale;
    return TRS3D_Rotate(local, rotation) + pivot + translation;
}

float3 TRS3D_InverseTransformPoint(float3 position, float3 translation, float4 rotation,
                                   float3 scale, float3 pivot)
{
    float3 local = TRS3D_InverseRotate(position - pivot - translation, rotation);
    return local / max(abs(scale), float3(1e-6, 1e-6, 1e-6)) * sign(scale) + pivot;
}

float2 TRS3D_ProjectCameraPoint(float3 cameraPoint, float focalLength,
                                float aspectRatio)
{
    float safeDepth = max(cameraPoint.z, 1e-4);
    float2 ndc;
    ndc.x = focalLength * cameraPoint.x / safeDepth / max(aspectRatio, 1e-4);
    ndc.y = focalLength * cameraPoint.y / safeDepth;
    return ndc * 0.5 + 0.5;
}

float3 TRS3D_UnprojectCameraUV(float2 uv, float depth, float focalLength,
                               float aspectRatio)
{
    float2 ndc = uv * 2.0 - 1.0;
    float safeFocal = max(focalLength, 1e-4);
    return float3(ndc.x * depth * aspectRatio / safeFocal,
                  ndc.y * depth / safeFocal,
                  depth);
}

#endif // TRS_HLSL_INCLUDED

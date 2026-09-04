using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Mine.RenderingDebug
{
    // ════════════════════════════════════════════════════════════
    //  DebugOutputFeature — 将绑定 Shader 直接覆盖到屏幕
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// 通用全屏 Shader 调试 RendererFeature。Feature 只负责创建真实 RenderGraph 临时 RT、
    /// 执行绑定 Shader 的全屏 Blit，并把结果写回相机颜色目标。
    /// </summary>
    public sealed class DebugOutputFeature : ScriptableRendererFeature
    {
        [Serializable]
        public sealed class Settings
        {
            public Shader debugShader;

            [Header("Debug")]
            public bool debug;
        }

        private sealed class ShaderPass : ScriptableRenderPass
        {
            private sealed class RenderPassData
            {
                public Material material;
                public TextureHandle source;
                public TextureHandle output;
            }

            private sealed class OutputPassData
            {
                public TextureHandle source;
                public TextureHandle destination;
            }

            private readonly Material _material;

            public ShaderPass(Material material)
            {
                _material = material;
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                TextureHandle destination = resourceData.activeColorTexture;

                if (_material == null || !destination.IsValid())
                    return;

                RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;
                descriptor.bindMS = false;

                TextureHandle output = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph,
                    descriptor,
                    "_DebugShaderOutputRT",
                    false);

                using (var builder = renderGraph.AddUnsafePass<RenderPassData>(
                    "Debug Output/Shader",
                    out var passData))
                {
                    passData.material = _material;
                    passData.source = destination;
                    passData.output = output;

                    builder.UseTexture(destination, AccessFlags.Read);
                    builder.UseTexture(output, AccessFlags.Write);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((RenderPassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer commandBuffer = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        Blitter.BlitCameraTexture(
                            commandBuffer,
                            data.source,
                            data.output,
                            data.material,
                            0);
                    });
                }

                using (var builder = renderGraph.AddUnsafePass<OutputPassData>(
                    "Debug Output/Screen",
                    out var passData))
                {
                    passData.source = output;
                    passData.destination = destination;

                    builder.UseTexture(output, AccessFlags.Read);
                    builder.UseTexture(destination, AccessFlags.ReadWrite);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((OutputPassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer commandBuffer = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        Blitter.BlitCameraTexture(commandBuffer, data.source, data.destination);
                    });
                }
            }
        }

        public Settings settings = new Settings();

        private ShaderPass _pass;
        private Material _ownedMaterial;

        public override void Create()
        {
            ReleaseResources();

            if (settings.debugShader != null)
            {
                _ownedMaterial = CoreUtils.CreateEngineMaterial(settings.debugShader);
                _pass = new ShaderPass(_ownedMaterial);
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.debug && _pass != null)
                renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ReleaseResources();

            base.Dispose(disposing);
        }

        private void ReleaseResources()
        {
            _pass = null;

            if (_ownedMaterial != null)
            {
                CoreUtils.Destroy(_ownedMaterial);
                _ownedMaterial = null;
            }
        }
    }
}

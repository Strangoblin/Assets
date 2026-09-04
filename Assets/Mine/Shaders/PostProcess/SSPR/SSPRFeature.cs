using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class SSPRFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader ssprShader;

        [Header("Trace RT")]
        [Range(0, 3)] public int traceDownsample = 1;  // 0=full, 1=half, 2=quarter, 3=eighth

        [Header("Distant Flip")]
        [Range(1, 200)] public float maxDistance = 50f;
        [Range(0f, 0.95f)] public float flipFade = 0.3f;   // 远景淡入起点（maxDistance 的倍数）

        [Header("Appearance")]
        [Range(0, 1)] public float smoothness = 1f;
        public bool showReflection = true;
    }

    class SSPRPass : ScriptableRenderPass
    {
        Material mat;
        Settings cfg;

        class PassData
        {
            public Material mat;
            public TextureHandle src, refl;
            public bool show;
        }

        public SSPRPass(Shader s, Settings c)
        {
            cfg = c;
            if (s) mat = CoreUtils.CreateEngineMaterial(s);
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        public override void RecordRenderGraph(RenderGraph rg, ContextContainer frameData)
        {
            var rd = frameData.Get<UniversalResourceData>();
            var cd = frameData.Get<UniversalCameraData>();
            var src = rd.activeColorTexture;
            if (!src.IsValid() || !mat) return;

            mat.SetFloat("_MaxDistance", cfg.maxDistance);
            mat.SetFloat("_FlipFade", cfg.maxDistance * cfg.flipFade);
            mat.SetFloat("_Smoothness", cfg.smoothness);
            mat.SetMatrix("_CameraViewMatrix", cd.GetViewMatrix());
            mat.SetMatrix("_CameraProjectionMatrix", GL.GetGPUProjectionMatrix(cd.GetProjectionMatrix(), true));

            var desc = cd.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.colorFormat = RenderTextureFormat.ARGBHalf;
            desc.width  >>= cfg.traceDownsample;
            desc.height >>= cfg.traceDownsample;
            var refl = UniversalRenderer.CreateRenderGraphTexture(rg, desc, "_SSPR_ReflectRT", false);

            using (var b = rg.AddUnsafePass<PassData>("SSPR", out var pd))
            {
                pd.mat = mat; pd.src = src; pd.refl = refl; pd.show = cfg.showReflection;
                b.UseTexture(src, AccessFlags.ReadWrite);
                b.UseTexture(refl, AccessFlags.ReadWrite);
                b.AllowPassCulling(false);

                b.SetRenderFunc((PassData d, UnsafeGraphContext ctx) =>
                {
                    var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                    Blitter.BlitCameraTexture(cmd, d.src, d.refl, d.mat, 0);
                    if (d.show)
                        Blitter.BlitCameraTexture(cmd, d.refl, d.src, d.mat, 1);
                });
            }
        }
    }

    public Settings settings = new();
    SSPRPass pass;

    public override void Create()
    {
        pass = settings.ssprShader ? new SSPRPass(settings.ssprShader, settings) : null;
    }

    public override void AddRenderPasses(ScriptableRenderer r, ref RenderingData d)
    {
        if (pass != null) r.EnqueuePass(pass);
    }
}

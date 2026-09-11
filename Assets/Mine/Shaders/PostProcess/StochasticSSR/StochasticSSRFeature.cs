using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class StochasticSSRFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader stochasticSSRShader;

        [Header("Trace RT")]
        [Range(0, 3)] public int traceDownsample = 2;

        [Header("Ray March")]
        [Range(0.1f, 2f)] public float stepSize = 0.3f;
        [Range(1f, 200f)] public float maxDistance = 50f;
        [Range(8, 256)] public int stepCount = 64;
        [Range(0.001f, 0.5f)] public float thickness = 0.05f;

        [Header("Resolve")]
        [Range(0, 3)] public int resolveDownsample = 2;  // 0=full, 1=half, 2=quarter, 3=eighth
        [Range(0, 1)] public float roughness = 0.3f;
        [Range(1, 5)] public int resolveRadius = 2;
        public enum ResolveQuality { Low, Medium, High }
        public ResolveQuality resolveQuality = ResolveQuality.High;
        [Range(0, 1)] public float skyFallback = 0.4f;

        [Header("Temporal")]
        [Range(0, 1)] public float temporalBlend = 0.9f;

        public enum DebugMode { Off, Trace, Resolve }
        [Header("Debug")] public DebugMode debug = DebugMode.Off;
    }

    class SSSRPass : ScriptableRenderPass
    {
        Material mat;
        Settings cfg;
        RTHandle historyA, historyB;
        int frameIndex;

        class PassData
        {
            public Material mat;
            public TextureHandle src, trace, resolve, temporalOut;
            public TextureHandle motionVec, historyRead;
            public RTHandle historyWrite;
            public bool isDebug;
        }

        void EnsureHistory(int w, int h)
        {
            if (historyA != null && historyA.rt != null && historyA.rt.width == w && historyA.rt.height == h)
                return;
            historyA?.Release(); historyB?.Release();
            var rtA = new RenderTexture(w, h, 0, RenderTextureFormat.ARGBHalf);
            var rtB = new RenderTexture(w, h, 0, RenderTextureFormat.ARGBHalf);
            rtA.filterMode = FilterMode.Bilinear; rtB.filterMode = FilterMode.Bilinear;
            rtA.Create(); rtB.Create();
            historyA = RTHandles.Alloc(rtA); historyB = RTHandles.Alloc(rtB);
        }

        public SSSRPass(Shader s, Settings c)
        {
            cfg = c;
            if (s) mat = CoreUtils.CreateEngineMaterial(s);
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal | ScriptableRenderPassInput.Motion);
        }

        public override void RecordRenderGraph(RenderGraph rg, ContextContainer frameData)
        {
            var rd = frameData.Get<UniversalResourceData>();
            var cd = frameData.Get<UniversalCameraData>();
            var src = rd.activeColorTexture;
            if (!src.IsValid() || !mat) return;

            int w = cd.cameraTargetDescriptor.width;
            int h = cd.cameraTargetDescriptor.height;
            EnsureHistory(w, h);

            mat.SetFloat("_StepSize", cfg.stepSize);
            mat.SetFloat("_MaxDistance", cfg.maxDistance);
            mat.SetInt("_StepCount", cfg.stepCount);
            mat.SetFloat("_Thickness", cfg.thickness);
            mat.SetFloat("_Roughness", cfg.roughness);
            mat.SetInt("_ResolveRadius", cfg.resolveRadius);
            mat.SetInt("_ResolveQuality", (int)cfg.resolveQuality);
            mat.SetFloat("_TemporalBlend", cfg.temporalBlend);
            mat.SetFloat("_SkyFallback", cfg.skyFallback);
            mat.SetFloat("_FrameIndex", frameIndex);
            mat.SetMatrix("_CameraViewMatrix", cd.GetViewMatrix());
            mat.SetMatrix("_CameraProjectionMatrix",
                GL.GetGPUProjectionMatrix(cd.GetProjectionMatrix(), true));

            var desc = cd.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.colorFormat = RenderTextureFormat.ARGBHalf;

            var tDesc = desc;
            tDesc.width  >>= cfg.traceDownsample;
            tDesc.height >>= cfg.traceDownsample;
            var traceRT = UniversalRenderer.CreateRenderGraphTexture(rg, tDesc, "_SSSR_TraceRT", false);

            var rDesc = desc;
            rDesc.width  >>= cfg.resolveDownsample;
            rDesc.height >>= cfg.resolveDownsample;
            mat.SetVector("_ResolveTexelSize", new Vector4(1f / rDesc.width, 1f / rDesc.height, 0, 0));
            var resolveRT = UniversalRenderer.CreateRenderGraphTexture(rg, rDesc, "_SSSR_ResolveRT", false);
            var temporalOutRT = UniversalRenderer.CreateRenderGraphTexture(rg, desc, "_SSSR_TemporalRT", false);
            var motionVec = rd.motionVectorColor;

            // Ping-pong history
            var historyRead = (frameIndex % 2 == 0) ? historyA : historyB;
            var historyWrite = (frameIndex % 2 == 0) ? historyB : historyA;
            var historyReadTH = rg.ImportTexture(historyRead);
            frameIndex++;

            // Keywords
            mat.DisableKeyword("SSSR_DEBUG_TRACE");
            bool isDebug = cfg.debug != Settings.DebugMode.Off;
            if (cfg.debug == Settings.DebugMode.Trace) mat.EnableKeyword("SSSR_DEBUG_TRACE");

            using (var b = rg.AddUnsafePass<PassData>("SSSR", out var pd))
            {
                pd.mat = mat; pd.src = src; pd.trace = traceRT; pd.resolve = resolveRT;
                pd.temporalOut = temporalOutRT; pd.motionVec = motionVec;
                pd.historyRead = historyReadTH; pd.historyWrite = historyWrite;
                pd.isDebug = isDebug;

                b.UseTexture(src, AccessFlags.ReadWrite);
                b.UseTexture(traceRT, AccessFlags.ReadWrite);
                b.UseTexture(resolveRT, AccessFlags.ReadWrite);
                b.UseTexture(temporalOutRT, AccessFlags.ReadWrite);
                b.UseTexture(historyReadTH, AccessFlags.Read);
                if (motionVec.IsValid()) b.UseTexture(motionVec, AccessFlags.Read);
                b.AllowPassCulling(false);

                b.SetRenderFunc((PassData d, UnsafeGraphContext ctx) =>
                {
                    var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);

                    // Stage 1: Trace (GGX 1-ray, low-res)
                    Blitter.BlitCameraTexture(cmd, d.src, d.trace, d.mat, 0);

                    // Stage 2: Resolve (spatial gather → full-res)
                    cmd.SetGlobalTexture("_TraceTex", d.trace);
                    Blitter.BlitCameraTexture(cmd, d.trace, d.resolve, d.mat, 1);

                    // Stage 3: Temporal (motion reprojection + history blend)
                    cmd.SetGlobalTexture("_HistoryTex", d.historyRead);
                    cmd.SetGlobalTexture("_MotionVecTex", d.motionVec);
                    Blitter.BlitCameraTexture(cmd, d.resolve, d.temporalOut, d.mat, 2);

                    // Save temporal output to history RTHandle for next frame
                    cmd.CopyTexture(d.temporalOut, d.historyWrite.nameID);

                    // Output
                    if (d.isDebug)
                        Blitter.BlitCameraTexture(cmd, d.temporalOut, d.src, d.mat, 3);
                    else
                        Blitter.BlitCameraTexture(cmd, d.temporalOut, d.src);
                });
            }
        }

        public void Release()
        {
            historyA?.Release(); historyB?.Release();
            historyA = null; historyB = null;
        }
    }

    public Settings settings = new();
    SSSRPass pass;

    public override void Create()
    {
        pass?.Release();
        pass = settings.stochasticSSRShader ? new SSSRPass(settings.stochasticSSRShader, settings) : null;
    }

    protected override void Dispose(bool disposing)
    {
        pass?.Release();
        pass = null;
    }

    public override void AddRenderPasses(ScriptableRenderer r, ref RenderingData d)
    {
        if (pass != null) r.EnqueuePass(pass);
    }
}

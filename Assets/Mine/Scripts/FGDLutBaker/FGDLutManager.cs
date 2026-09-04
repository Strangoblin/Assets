using UnityEngine;

namespace Mine.FGDLutBaker
{
    /// <summary>
    /// FGD LUT 场景管理器 — 挂载到场景后自行探测 Inspector 上的 LUT 纹理：
    /// 有图则设为全局（_FGDLut + _UseFGDLut = 1），无图则清除并回退 Karis 分析近似。
    /// 与烘焙工具 FGDLutBaker 完全解耦：Baker 只负责生产纹理，本类不做任何烘焙。
    /// </summary>
    ///
    /// <remarks>
    /// 生命周期：
    ///   OnEnable  → 探测并应用
    ///   OnDisable → 清除全局，Shader 回退到 Karis 分析近似
    ///   OnValidate → Inspector 修改 LUT 时立即刷新（编辑态同样生效）
    ///
    /// 不需要任何外部驱动，赋图/取图即自动生效。
    /// </remarks>
    [ExecuteAlways]
    public sealed class FGDLutManager : MonoBehaviour
    {
        [Header("LUT")]
        [SerializeField] private Texture2D _lut;

        // ── Shader Property IDs ─────────────────────────────────

        private static readonly int s_LutID    = Shader.PropertyToID("_FGDLut");
        private static readonly int s_UseLutID = Shader.PropertyToID("_UseFGDLut");

        // ════════════════════════════════════════════════════════
        //  生命周期（自行探测 LUT）
        // ════════════════════════════════════════════════════════

        private void OnEnable()
        {
            Apply();
        }

        private void OnDisable()
        {
            Clear();
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled) Apply();
        }

        // ════════════════════════════════════════════════════════
        //  全局状态
        // ════════════════════════════════════════════════════════

        private void Apply()
        {
            if (_lut == null)
            {
                Clear();
                return;
            }

            Shader.SetGlobalTexture(s_LutID, _lut);
            Shader.SetGlobalFloat(s_UseLutID, 1.0f);
            Debug.Log($"FGDLutManager: Global _FGDLut set ({_lut.width}×{_lut.height}). "
                + "All ENVFunction shaders now use FGD LUT path.");
        }

        private void Clear()
        {
            Shader.SetGlobalTexture(s_LutID, null);
            Shader.SetGlobalFloat(s_UseLutID, 0.0f);
            Debug.Log("FGDLutManager: Global _FGDLut cleared. Shaders fallback to analytical.");
        }
    }
}

# Window Templates

**定位**:编辑器工具窗口壳(EditorWindow)——跨 baker/generator 的共享形态。窗口只做 GUI 壳与资产落盘,**不承载生成逻辑**(逻辑在家族服务层:facade / baker service)。

**族内结构**:窗口壳 [editor-window.cs](editor-window.cs)(通用骨架)+ 本 README 消费差异。窗口不是任何单一职责族的专有物,baker/generator/任意工具都消费它——归属裁定见 `memory/2026-09-03-standard-code-window-family.md`。

**通用骨架要点**(见 [editor-window.cs](editor-window.cs)):
- 菜单入口 + `GetWindow<T>(false, title, true)`;类 `sealed` + `: EditorWindow`
- 窗口脊柱:标题(boldLabel)→ 设置分区(boldLabel/box 分块,`[SerializeField]` 字段 `_` 前缀)→ 输出路径行 + `[Browse]` → 行动行 → Preview/Info
- 行动按钮统一 `Height(30)`;行动行禁用态:`DisabledScope` 包条件(如无预览纹理时 Save 禁用)
- 输出路径默认 `Assets/Mine/<Area>/<Name>.<ext>`;Browse 走 `EditorUtility.SaveFilePanelInProject`
- Save 前覆盖确认(`DisplayDialog`),副本 `Instantiate` 后 `DeleteAsset` + `CreateAsset` + `Ping`
- 归位 `Assets/Mine/Scripts/<Tool>/Editor/`;UI 文案英文
- **纹理所有权**:本窗生成 → 窗口拥有,OnDisable/换源时 `DestroyImmediate`;AssetDatabase Load 的 → 绝不销毁

**消费差异**(同窗骨架,按职责族改写行动行):

| 族 | 行动行 | 特有行为 | 服务层 |
|---|---|---|---|
| Baker | `[Bake][Load][Save]` | Bake 带进度条(`DisplayProgressBar` + try/finally);Load 回看已存资产;一次性不可再生 | [baker/baker-service.cs](../baker/baker-service.cs) |
| Generator | `[Generate][Save]` | 预览带 hash 缓存(参数不变不重建);可反复再生成 | [generator/generator-facade.cs](../generator/generator-facade.cs) |

**项目范例**:

| 范例 | 路径 | 学习点 |
|------|------|--------|
| NoiseGenerator Window | `Assets/Mine/Scripts/NoiseGenerator/Editor/` | 标准形:模式 toolbar + 设置分区 + hash 缓存预览 |
| CurveGenerator Window | `Assets/Mine/Scripts/CurveGenerator/Editor/` | Scene 预览形态 |
| InteriorMap Baker Window | `Assets/Mine/Scripts/InteriorMapBaker/Editor/InteriorMapBakerWindow.cs` | Bake/Load/Save + 状态 HelpBox + 2:1 预览 |
| FGD LUT Baker Window | `Assets/Mine/Scripts/FGDLutBaker/Editor/FGDLutBakerWindow.cs` | 精简形态(无框架引用,可无 Load) |

> ⚠️ 新窗口归位 `<Tool>/Editor/` 而非 `Assets/Editor/`。
> 双开注意:`EditorWindow` 脚本无需任何 URP/运行时依赖,纯 `UnityEditor` API。

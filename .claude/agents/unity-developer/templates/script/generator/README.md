# Generator Templates

**定位**:从参数/场景输入生成**可再生**资产(噪声纹理、曲线数据、网格等)的编辑器工具。改参数随时重出,结果通用、可反复 Save。

**族内结构**:核心层 [generator-facade.cs](generator-facade.cs)(静态 facade 骨架)+ 窗口壳 → 通用 [window/](../window/README.md) 家族(Generator 消费形态)。

**骨架要点**(标准形 = CurveGenerator / NoiseGenerator,2026-09-03 统一后实码即最佳模板):
- 核心 = 静态 facade 类(如 `NoiseGenerator`)提供生成 API(Editor 与 Runtime 均可调),窗口只做瘦壳
- 窗口归位 `Assets/Mine/Scripts/<Tool>/Editor/`,与 facade 同目录;配套中文 `.md` 文档同目录
- 窗口脊柱见 [window/](../window/README.md):标题 → 设置 → Output 路径行 + Browse → `[Generate][Save(DisabledScope)]` → Preview/Info
- Generator 特有:预览带 hash 缓存(参数不变拖动 slice 不重建);可再生 → 可无 Load
- 窗口字段 `_` 前缀 + `[SerializeField]`;UI 文案英文;默认路径 `Assets/Mine/<Area>/`
- 曲线类数据消费者走 `CurveAsset` 类 ScriptableObject(可 `CreateAssetMenu`)

**项目范例**:

| 范例 | 路径 | 学习点 |
|------|------|--------|
| NoiseGenerator | `Assets/Mine/Scripts/NoiseGenerator/` | facade(生成/打包/无缝)+ 窗口 + .md 完整标准形 |
| CurveGenerator | `Assets/Mine/Scripts/CurveGenerator/` | facade + 共享采样骨架 + parent-only 输入 + Scene 预览 |
| Grass Generator | `Assets/Editor/GrassGeneratorEditor.cs`(遗留位置) | 反例对照:窗口仍在 Assets/Editor,新工具勿仿 |

**与相邻族边界**:
- vs Baker:Generator 可再生、无需 Load/进度条;Baker 一次性重烘焙见 [baker/](../baker/README.md)
- 纯数据服务(无窗口需求)不属于 Generator,按 [script-structure.md](../../../references/standard/script/script-structure.md) 组织即可

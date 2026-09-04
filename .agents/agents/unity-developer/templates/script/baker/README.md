# Baker Templates

**定位**:一次性重烘焙资产的编辑器工具。结果依赖场景框架或重型计算,不可廉价再生,需要「生成后可回看已存资产」与进度条。

**族内结构(核心层)**:
- 服务层 [baker-service.cs](baker-service.cs) — 无状态静态类,单一 `Bake()` 入口产出纹理;GPU 烘焙 + try/finally 清理,所有权移交调用方
- 窗口壳 → 通用 [window/](../window/README.md) 家族(Baker 消费形态);baker 不再独有窗口模板

**骨架要点**(bake 消费形态,通用壳见 [window/](../window/README.md)):
- 行动行 `[Bake][Load][Save]`,按钮统一 Height(30);Bake 禁用态取决于框架就绪,Save 禁用态取决于当前有预览纹理
- Bake 用 `DisplayProgressBar` 进度条 + try/finally(Baker 特有,Generator 无);一次性重烘焙 → 需 Load 回看
- 烘焙委托给可复用服务类(如 `InteriorMapTextureBaker`),窗口只做壳

**项目范例**:

| 范例 | 路径 | 学习点 |
|------|------|--------|
| InteriorMap Baker | `Assets/Mine/Scripts/InteriorMapBaker/`（含 `Editor/InteriorMapBakerWindow.cs`） | 框架 ObjectField + 状态 HelpBox + 2:1 预览；组件与窗口均归位工具目录 `Editor/` |
| FGD LUT Baker | `Assets/Mine/Scripts/FGDLutBaker/`（含 `Editor/FGDLutBakerWindow.cs`） | 纯参数型(无框架,无覆盖确认),可删 Load 的精简形态 |

**与相邻族边界**:
- vs Generator:Generator 生成**可再生**资产(改参数随时重出,结果通用);Baker 一次性重烘焙(场景/环境依赖,结果不可再生),故需 Load 回看与进度条
- vs Manager:Manager 是运行时组件不做资产产出;Baker 是编辑器工具,产 `Texture2D/Texture3D` 资产

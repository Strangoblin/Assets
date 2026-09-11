# Snowy — 单层 UV 网格落雪实验

URP 17.3 全屏后处理原型。当前采用原始 0–1 UV：单粒子采样区域宽、高各为 0.1，面积为屏幕的 1%。雪花纹理的透明留白使实际可见形状略小于采样区域。

## 文件与入口

- `Snowy.shader`：`Mine/PostProcess/Snowy`，场景颜色与粒子 RGBA 合成。
- `SnowyParticle.hlsl`：连续位移、网格分割、逆旋转、缩放和边界遮罩。
- `SnowyParticle.png`：64×64 RGBA 数学构造雪花；可换成自己的 RGBA 粒子图。
- `Snowy_Single.mat`：中央静止单粒子，采样区域 0.1×0.1 UV。
- `Snowy_Grid.mat`：10×10 网格，格内采样区域 0.35×0.35，屏幕 UV 尺寸为 0.035×0.035，开启下落与旋转。

当前 `Assets/Settings/PC_Renderer.asset` 已添加 **Snowy Experiment**（Unity 内置 Full Screen Pass Renderer Feature），当前绑定 **Snowy_Grid.mat**（Animate 开启）。在 Renderer 的 **Pass Material** 中切换为 **Snowy_Grid.mat** 即可观察单层落雪。停止整个实验可关闭该 Feature 的 Active。

Feature 配置：After Rendering Post Processing、Fetch Color Buffer 开启、Requirements None、Pass Index 0。不需要深度和法线。URP 17.3 的 Fetch Color Buffer 将场景色提供给 `_BlitTexture`；旧版面板可能通过 Requirements Color 控制。
参考：[Unity Full Screen Pass 文档](https://docs.unity3d.com/cn/6000.0/Manual/urp/renderer-features/renderer-feature-full-screen-pass.html)，具体字段已对照本项目 URP 包源码。

## 参数

| 参数 | 含义 |
|---|---|
| Particle RGBA | RGB 作为粒子颜色，Alpha 作为遮罩；没有透明通道的白图将显示方块 |
| Particle Color | 粒子染色与整体透明度 |
| Particle Size | 格内粒子采样区域的基础宽高；Single 模式等同屏幕 UV |
| Scale XY | 粒子局部 X/Y 缩放；零值隐藏粒子；负值保留符号，可镜像纹理 |
| Rotation | 围绕粒子中心的角度；UV 空间逆时针为正 |
| Layout | Single：一个粒子；Grid：每格一个粒子 |
| Grid | 列、行数，四舍五入且至少为 1 |
| Offset XY | 屏幕 UV 位移；正 X 向右，正 Y 向上；不随网格密度改变单位 |
| Animate | 开启时使用 Unity `_Time.y`，不会在勾选时重置时钟 |
| Preview Time | 额外时间偏移；关闭 Animate 后可手动控制动画时间 |
| Velocity XY | 每秒屏幕 UV 位移；默认 (0.015, -0.06)，向右下 |
| Rotation Speed | 每秒旋转角度 |
| Debug View | Composite：合成；ParticleMask：黑底遮罩；ParticleUV：采样区域显示红绿 UV |
| Show Grid | 固定于屏幕的网格线，便于观察移动中的粒子跨越原网格位置 |

Edit Mode 下动画刷新频率取决于 Editor 重绘；可用 Preview Time 精确检查，Play Mode 可连续观察。

## 核心变换

```text
连续坐标 = 屏幕 UV - Offset - Velocity × 时间 - 垂直方向 × 横摆幅度 × sin(2π × 横摆频率 × 时间)
格内坐标 = frac(连续坐标 × Grid) - 0.5     // Single 则为连续坐标 - 0.5
相位 = 移动网格编号的稳定哈希 × 2π × PhaseVariation
角度 = Rotation + RotationSpeed × 时间 + RotationAmplitude × [sin(2π × RotationFrequency × 时间 + 相位) - sin(相位)]
缩放倍率 = 1 + ScaleAmplitude × sin(2π × ScaleFrequency × 时间 + 相位)
粒子 UV = 逆旋转(格内坐标, 角度) / (ParticleSize × ScaleXY × 缩放倍率) + 0.5
输出 = lerp(场景颜色, 粒子 RGB × 颜色, 粒子 Alpha × 范围遮罩)
```

`SnowyParticleUV` 输出粒子 UV 与有效范围遮罩；`SnowyInverseRotate` 负责逆旋转；`SnowyGridLines` 输出固定屏幕网格线。

缩放作用于粒子自己的坐标轴，随后在 UV 空间旋转，最后平移。Grid 模式先连续平移再取 frac，所以整个单层可以无限滚动。Single 不循环，移出屏幕后消失。

每轴最终采样区域尺寸限制在 0.0001–0.7 cell。最大旋转包围盒为 0.7×√2 < 1 cell，因此单次纹理采样即可避免相邻格边界切断粒子。原型不支持大于格子的粒子、独立随机中心或粒子重叠；扩展这些能力时应加入邻格采样和透明合成。

场景色使用 XR 对应的 `TEXTURE2D_X`；粒子图是普通共享 2D 资产，使用普通 `TEXTURE2D`。粒子显式采样 mip 0 避免 frac 接缝的隐式导数问题，远距离小粒子的过滤优化留待后续。

## 建议观察顺序

1. Single 材质将 Debug View 设为 ParticleUV：确认中心 0.1×0.1 区域。
2. Scale XY 设为 (2,1)，Rotation 设为 90：确认长方形旋转。
3. Offset 设为 (0.2,-0.1)：中心移动到 (0.7,0.4)。
4. 切换 Grid 材质，开启 Show Grid：观察单层粒子连续跨过固定网格。
5. Debug View 回到 Composite，使用自己的 RGBA 雪花纹理继续调试。

当前不补偿屏幕宽高比，因此横屏中相等的 UV 宽高不是相等的像素宽高；这是默认 UV 实验的预期行为。已实现旋转和缩放的逐格相位随机化；未实现随机位置、多层、相机深度遮挡或世界空间落雪。

## 验证记录

2026-09-07，Unity Editor / Metal：

- Shader 强制重新导入后 supported=True，ShaderUtil 无错误消息。
- 512×512 GPU 读回：单粒子采样范围约 0.1016×0.1016（像素取整误差），中心 (0.5,0.5)。
- 2×1 缩放再旋转 90°：宽高约 0.1016×0.1992。
- Offset (0.2,-0.1)：中心约 (0.7002,0.4004)。
- 速度 (0,-0.1)、时间 1 秒：中心约 (0.5,0.3994)。
- 10×10 网格：检测到 100 个独立粒子。
- Scale=0：保留场景 RGBA。
- 主相机经 URP SingleCameraRequest 渲染：启用网格落雪后 13437 个像素变化，验证期间无新增错误。
- 临时渲染资源已释放，主相机验证后恢复 Single 材质，未保存当前已修改的场景。

验证脚本和截图位于 `.codex/tmp/snowy-*.cs` / `.codex/tmp/snowy-*-preview.png`。接入前 Renderer 快照位于 `.codex/tmp/snowy-before/PC_Renderer.asset`。

## 时间扰动（2026-09-07）

当前默认演示材质为 Snowy_Grid，已开启 Animate。移动方向和速度沿用 Velocity XY；默认右下移动。

| 新参数 | 默认值 | 作用 |
|---|---|---|
| Sway Amplitude | 0.008 UV | 整层在移动方向的垂直方向往复摆动；所有粒子共享平移，保持格间连续 |
| Sway Frequency | 0.35 Hz | 横向摆动周期频率 |
| Rotation Amplitude | 20° | 在匀速旋转上叠加角度摆动；以 t=0 为基准，变化量最多为两倍幅度 |
| Rotation Frequency | 0.4 Hz | 角度摆动频率 |
| Scale Amplitude | 0.3 | 缩放倍率在 0.7–1.3 间变化；上限 0.95，避免负缩放和消失 |
| Scale Frequency | 0.5 Hz | 缩放周期频率 |
| Phase Variation | 1 | 按随粒子移动的网格编号分配旋转、缩放相位；0 为所有粒子同步 |

零速度时，横向摆动使用 X 轴；要完全停住平移，还需将 Sway Amplitude 设为 0。
关闭 Animate 后使用 Preview Time 可逐点检查；三个 Amplitude 设为 0 可恢复原来的匀速平移、旋转和固定缩放。
粒子尺寸仍限制为每轴不超过 0.7 cell，接近上限时缩放波峰会被截平，以维持格边界安全。
Single 模式没有逐格随机相位；Grid 模式相位随粒子而非屏幕位置保持固定，周期性哈希在 289 格后重复。

新增 GPU 验证：t=0.25 的横向摆动中心约 (0.5205,0.4756)；零速度摆动保持有限值；缩放周期在四分之一/四分之三处得到约 0.1484/0.0508 UV；旋转扰动四分之一周期正确得到 90°。旧变换测试关闭扰动后全部通过。主相机重新渲染后 14887 个像素变化，无新增错误。测试恢复原先绑定的 Grid 材质。

## 缩放与旋转模拟 3D 翻面

Snowy_Grid 已开启 Enable 3D Flip，默认绕局部 Y 轴，速度 90°/秒。Snowy_Single 默认不翻面，可单独开启检查。

| 参数 | 作用 |
|---|---|
| Enable 3D Flip | 启用薄片的正交投影翻面 |
| Flip Axis | AroundY 压缩局部宽度；AroundX 压缩局部高度 |
| Initial Flip Degrees | 初始翻面角度 |
| Flip Degrees Per Second | 翻面角速度；负数反向；默认 90°/秒即 4 秒一圈 |

投影系数为 cos(初始角度 + 时间 × 角速度 + 逐格相位)，按选定轴乘到尺寸上，再叠加已有的平面旋转。
0° → 正面完整，60° → 对应轴约半宽/半高，90° → 侧立，180° → 背面镜像，360° → 回到正面。
余弦负值保留，采样 UV 也保留缩放符号；分母设最小绝对值，投影绝对值低于 0.02 时平滑降低可见度，避免侧立时除零和残留细线。
原有 Scale Pulse 仍控制整体大小变化，Flip 控制随朝向变化的投影压缩；两者与平面 Rotation 一起生效。
关闭 Animate 后可用 Preview Time 检查姿态。精确检查单粒子时先关闭平移、旋转速度、扰动，使用 Initial Flip Degrees。
这是屏幕 UV 空间中薄片的正交投影模拟，沿用原始 UV 比例，不包含透视、厚度、背面独立贴图或三维光照。

验证：60° 的 X/Y 压缩与叠加 90° 平面旋转均通过 GPU 包围盒检查；90° 无残留亮线；时间驱动 180° 时左/右 UV 的 U 值分别约 0.745/0.255，确认背面镜像。主相机渲染有 8762 个像素变化，无新增错误。所有此前变换测试通过。

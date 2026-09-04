# Roslyn Script Execution Reference

> C# Roslyn 脚本模板 + unityctl 命令速查。CLI 层文档。

---

## 快速命令速查

```bash
# 状态检查
unityctl status

# 编译
unityctl asset refresh

# Play Mode
unityctl play enter
unityctl play exit

# 日志（进入 Play Mode 后自动清除，只显示当前会话日志）
unityctl logs -n 30
unityctl logs --stack       # 带堆栈

# 场景
unityctl scene list
unityctl scene load Assets/Scenes/xxx.unity

# 执行脚本
unityctl script execute -f path/to/script.cs
```

---

## 调试食谱

### 读取 Shader 编译报错（Shader Inspector 同源）

> Shader 编译错误不进控制台日志——由 ShaderUtil 持有，Shader Inspector 显示的即 `ShaderUtil.GetShaderMessages`。改完 .shader 后先编译再读取：

```bash
unityctl asset refresh
unityctl script eval -u UnityEditor 'var s = UnityEngine.Shader.Find("Mine/Render/HyperSpace"); var msgs = UnityEditor.ShaderUtil.GetShaderMessages(s); return msgs == null || msgs.Length == 0 ? "clean" : string.Join("\n", System.Array.ConvertAll(msgs, m => m.severity + " L" + m.line + ": " + m.message));'
```

### 运行时检查组件状态

```bash
unityctl script execute -f tmp/debug_state.cs
```

### 运行时执行方法

```csharp
// 调用任意 public 方法
go.GetComponent<SomeComponent>().SomePublicMethod();
```

---

## 脚本模板

可复用的 Roslyn 脚本位于 `scripts/roslyn/`：

| 脚本 | 用途 |
|------|------|
| `scene-query.cs` | 场景层级遍历（含组件信息 + active状态） |
| `scene-organize.cs` | 测试物体分组整理到 __TestObjects__ |
| `pipeline-check.cs` | 渲染管线 + Quality Level 诊断 |
| `scan-temp-objects.cs` | 扫描临时物体 |

---

## 引用

- RendererFeature 屏幕调试方法：[script-structure.md](../references/shader/postprocess/feature-script-structure.md)（Feature 自带 Debug 输出约定 + 失败定位顺序）
- 固定调试 Feature：`Assets/Mine/Scripts/Debug/DebugOutputFeature`（独立全屏 Shader 调试，Inspector 配置）
- 完整 unityctl 命令参考：[unityctl.md](unityctl.md)
- 场景配置脚本模板：../skills/auto-manager/capabilities/scene-setup.md
- 清理 Roslyn 脚本：../skills/auto-manager/capabilities/cleanup.md
- 可复用脚本策略 (`scripts/roslyn/`)：../skills/auto-manager/capabilities/cleanup.md

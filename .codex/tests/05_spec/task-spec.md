# 任务说明书 — 05_spec

## 目标
在 `output/` 目录下生成 2 个文件：一个 C# 工具类 + 一个使用说明 markdown。

## 要求

### 1. output/PathUtils.cs
```csharp
namespace CodexTest;

public static class PathUtils
{
    public static string Combine(string a, string b) => $"{a.TrimEnd('/')}/{b.TrimStart('/')}";
}
```

### 2. output/USAGE.md
内容必须包含以下 3 行：
```
# PathUtils
Combine 方法用于拼接路径
示例: PathUtils.Combine("a/b", "c") -> "a/b/c"
```

## 约束
- 只允许在 output/ 目录内创建文件
- 内容按上述模板逐字生成

## 验收标准
- output/PathUtils.cs 存在且包含 `Combine`
- output/USAGE.md 存在且包含 `示例`

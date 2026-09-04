# 03_write — 写文件能力

## 目的
验证 workspace-write 沙箱下 Codex 能创建文件。

## 指令
```
在 output/ 目录下创建 HelloWorld.cs，内容如下（必须逐字一致）：

namespace CodexTest;

public static class HelloWorld
{
    public static string Greet() => "Hello from Codex";
}
```

## 预期
`output/HelloWorld.cs` 存在且包含 `class HelloWorld`。

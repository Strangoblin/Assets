# 04_batch — 批量多文件任务

## 目的
验证 Codex 一次执行多文件操作的能力（批量工作，orchestrate 的核心场景）。

## 指令
```
在 output/ 目录下批量创建 3 个文件，每个文件内容按模板生成：

1. Alpha.cs   — namespace CodexTest; public static class Alpha { public const string Name = "Alpha"; }
2. Beta.cs    — namespace CodexTest; public static class Beta { public const string Name = "Beta"; }
3. Gamma.cs   — namespace CodexTest; public static class Gamma { public const string Name = "Gamma"; }

直接创建，无需询问。
```

## 预期
output/ 下存在 Alpha.cs / Beta.cs / Gamma.cs，各自包含对应类名。

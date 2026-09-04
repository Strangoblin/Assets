#!/bin/bash
# 04_batch — 批量多文件任务（工作区 = 本测试目录）
cd "$(dirname "$0")"
codex exec --sandbox workspace-write \
  "在 output/ 目录下批量创建 3 个文件：
1. Alpha.cs — namespace CodexTest; public static class Alpha { public const string Name = \"Alpha\"; }
2. Beta.cs  — namespace CodexTest; public static class Beta { public const string Name = \"Beta\"; }
3. Gamma.cs — namespace CodexTest; public static class Gamma { public const string Name = \"Gamma\"; }
直接创建，无需询问。" \
  > run.log 2>&1
bash verify.sh
exit $?

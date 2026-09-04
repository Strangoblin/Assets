#!/bin/bash
# 03_write — 写文件能力（工作区 = 本测试目录，写操作限制在 output/）
cd "$(dirname "$0")"
codex exec --sandbox workspace-write \
  "在 output/ 目录下创建 HelloWorld.cs，内容如下（必须逐字一致）：

namespace CodexTest;

public static class HelloWorld
{
    public static string Greet() => \"Hello from Codex\";
}" \
  > run.log 2>&1
bash verify.sh
exit $?

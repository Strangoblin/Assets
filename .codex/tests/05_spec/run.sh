#!/bin/bash
# 05_spec — 任务说明书传递（工作区 = 本测试目录）
cd "$(dirname "$0")"
codex exec --sandbox workspace-write \
  "读取当前目录下的 task-spec.md 并完整执行。不需要询问，直接完成所有要求，遵守其中的约束与验收标准。" \
  > run.log 2>&1
bash verify.sh
exit $?

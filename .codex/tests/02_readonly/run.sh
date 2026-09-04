#!/bin/bash
# 02_readonly — 只读代码审查（工作区 = 项目根，read-only 不写文件）
SELF_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SELF_DIR/../../.." && pwd)"
cd "$PROJECT_ROOT"
codex exec --sandbox read-only \
  "审查 Assets/Mine/Scripts/TestAuto.cs，列出最多 3 个潜在问题（代码风格问题或逻辑隐患），每条一行，格式: '行号或位置: 问题描述'。不要修改任何文件。" \
  > "$SELF_DIR/run.log" 2>&1
bash "$SELF_DIR/verify.sh"
exit $?

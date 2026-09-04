#!/bin/bash
# 01_smoke — 链路冒烟
cd "$(dirname "$0")"
codex exec --skip-git-repo-check --sandbox read-only "Reply with exactly: LINK_OK" > run.log 2>&1
bash verify.sh
exit $?

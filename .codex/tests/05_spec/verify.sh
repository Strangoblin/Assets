#!/bin/bash
# PASS: PathUtils.cs 含 Combine 且 USAGE.md 含示例
ok=1
[ -f output/PathUtils.cs ] && grep -q "Combine" output/PathUtils.cs || ok=0
[ -f output/USAGE.md ] && grep -q "示例" output/USAGE.md || ok=0
exit $((1-ok))

#!/bin/bash
# PASS: 3 个文件都存在且类名正确
ok=1
for f in Alpha Beta Gamma; do
  [ -f output/$f.cs ] && grep -q "class $f" output/$f.cs || ok=0
done
exit $((1-ok))

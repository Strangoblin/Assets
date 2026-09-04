#!/bin/bash
# PASS: output/HelloWorld.cs 存在且包含类定义
[ -f output/HelloWorld.cs ] && grep -q "class HelloWorld" output/HelloWorld.cs && exit 0 || exit 1

#!/bin/bash
# PASS: run.log 包含审查结论关键词
grep -qiE "问题|issue|潜在|建议|:.*(错误|风险|improve|risk)" run.log && exit 0 || exit 1

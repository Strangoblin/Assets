#!/usr/bin/env python3
"""规范检查 CLI — 后果验证门禁的 Codex 对等通道.

Claude 侧: write_gated 写入时自动执行同一检查（新增行 diff）。
Codex 侧: 产出文件合入前自查，或 Claude merge 复查时使用。

用法:
  python .mcp/validation/check_norm.py <file>     # 检查磁盘文件（全量）
退出码:
  0 = 无 error 违规（warning 不阻断）
  1 = 有 error 违规
"""

import sys
import os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from norms import check_content


def main() -> int:
    args = [a for a in sys.argv[1:] if not a.startswith("-")]
    if len(args) != 1:
        print(__doc__)
        return 2

    target = args[0]
    if not os.path.isfile(target):
        print(f"ERROR: 文件不存在: {target}")
        return 2
    with open(target, encoding="utf-8") as f:
        content = f.read()

    # CLI 全量检查（无 diff 语义）— 与 write_gated 的新增行检查互补
    result = check_content(target, content, existing=None)
    errors, warnings = result["errors"], result["warnings"]

    if errors:
        print(f"✗ {target}: {len(errors)} 项阻断违规 (exit 1)")
        for v in errors:
            lines = f" L{v['lines']}" if v["lines"] else ""
            print(f"  [{v['level']}] {v['name']}{lines} — {v['detail']}")
            print(f"      来源: {v['source']}")
    else:
        print(f"✓ {target}: 规范检查通过")

    if warnings:
        print(f"  ⚠ {len(warnings)} 项提示（不阻断）:")
        for v in warnings:
            print(f"    [{v['level']}] {v['name']} L{v['lines']} — {v['detail']}")

    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())

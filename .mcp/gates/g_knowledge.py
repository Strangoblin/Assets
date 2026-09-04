"""G1.5: 知识加载证据校验 — 声明必须命中真实知识文件.

链统一为 [g_entry, g_knowledge] 后，本门禁是唯一实质门禁（后果验证的第一层）:
  COMPLETE = 高优先级必读已声明 + 所有声明条目可解析到真实文件

解析规则:
  1) 知识库匹配 — references/ 或 rules/ 下真实 .md（文件名或相对路径后缀）
  2) 项目内真实文件 — 声明条目在项目根下真实存在（如参考实现 .shader/.cs）
两者都不命中 = 声明了不存在的内容（编造文件名）→ DENIED。
"""

import os

HIGH_PRIORITY_FILES = ["shader-structure.md", "script-structure.md"]

ALLOWED_STATUS = {"COMPLETE"}  # PARTIAL 已废弃 — 未读全即 DENIED

PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
KB_ROOTS = [
    os.path.join(PROJECT_ROOT, ".agents", "agents", "unity-developer", "references"),
    os.path.join(PROJECT_ROOT, ".agents", "rules"),
]


def _kb_index() -> list[tuple[str, str]]:
    """知识库索引: [(相对路径, 绝对路径)] — 只收 .md."""
    index = []
    for root in KB_ROOTS:
        if not os.path.isdir(root):
            continue
        for dirpath, _, files in os.walk(root):
            for f in files:
                if f.endswith(".md"):
                    full = os.path.join(dirpath, f)
                    rel = os.path.relpath(full, PROJECT_ROOT)
                    index.append((rel, full))
                    index.append((f, full))  # 文件名直配（跨目录同名取第一个）
    return index


def _resolve(entry: str, kb: list[tuple[str, str]]) -> str | None:
    """声明条目 → 绝对路径. 知识库后缀/文件名匹配, 或项目内真实文件."""
    e = entry.strip()
    if not e:
        return None
    for key, full in kb:
        if key == e or key.endswith("/" + e):
            return full
    full = os.path.join(PROJECT_ROOT, e)
    if os.path.isfile(full):
        return full
    return None


def _missing(loaded: list[str]) -> list[str]:
    """未声明已读的高优先级文件（子串匹配，兼容路径前缀）."""
    return [f for f in HIGH_PRIORITY_FILES
            if not any(f in l for l in loaded)]


def check(ctx: dict) -> dict:
    status = ctx.get("status", "").strip().upper()
    loaded = [f.strip() for f in ctx.get("loaded_files", "").split(",") if f.strip()]

    if status not in ALLOWED_STATUS:
        return {
            "status": "DENIED",
            "error": "G15_INVALID_STATUS",
            "hint": f"status 必须为 {sorted(ALLOWED_STATUS)} 之一。收到: '{status}'。"
                    f"格式: gate_pass('g_knowledge', loaded_files='shader-structure.md, script-structure.md', status='COMPLETE')",
        }

    # COMPLETE → 必须声明已读全部高优先级文件
    missing = _missing(loaded)
    if missing:
        return {
            "status": "DENIED",
            "error": "G15_MISSING_HIGH_PRIORITY",
            "hint": f"Status=COMPLETE 但未声明已读: {missing}。loaded_files 示例: '{', '.join(HIGH_PRIORITY_FILES)}'",
        }

    # 全部声明条目必须解析到真实文件（知识库或项目内）
    kb = _kb_index()
    unresolved = [e for e in loaded if _resolve(e, kb) is None]
    if unresolved:
        return {
            "status": "DENIED",
            "error": "G15_UNRESOLVED_FILE",
            "hint": f"以下声明条目无法解析到真实文件: {unresolved}。"
                    f"知识库位于 references/ 与 rules/；参考实现等代码文件用项目相对路径声明。",
        }

    return {
        "status": "OK",
        "knowledge": "COMPLETE",
        "loaded": loaded,
        "resolved": [_resolve(e, kb) for e in loaded],
    }

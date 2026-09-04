"""G1.5: 知识加载证据校验 — 声明必须命中真实知识文件.

知识 ID 优先使用 ``unity/<canonical-relative-path>`` 或
``rules/<canonical-relative-path>``；basename 只保留向后兼容，并且必须唯一。
"""

from __future__ import annotations

from pathlib import Path

from validation.knowledge_paths import (
    AmbiguousKnowledgePath,
    KnowledgePathError,
    default_kb_roots,
    find_project_root,
    resolve_entry,
)

HIGH_PRIORITY_FILES = [
    "unity/standard/shader/shader-structure.md",
    "unity/standard/script/script-structure.md",
]
ALLOWED_STATUS = {"COMPLETE"}
PROJECT_ROOT = find_project_root(__file__)
KB_ROOTS = default_kb_roots(PROJECT_ROOT)


def _kb_index() -> list[tuple[str, str]]:
    """兼容旧内部调用的索引视图: [(知识 ID, 绝对路径)]."""
    result: list[tuple[str, str]] = []
    for domain, root in KB_ROOTS:
        if not root.is_dir():
            continue
        for path in sorted(root.rglob("*.md")):
            result.append((f"{domain}/{path.relative_to(root).as_posix()}", path.as_posix()))
    return result


def _resolve(entry: str, kb: list[tuple[str, str]]) -> str | None:
    """解析单条知识声明；歧义和缺失统一返回 None，由 check 提供错误码."""
    del kb  # 解析必须走规范根和稳定 ID，不能依赖 basename 列表顺序。
    try:
        return resolve_entry(entry, PROJECT_ROOT, KB_ROOTS).resolved_path.as_posix()
    except KnowledgePathError:
        return None


def _missing(loaded: list[str]) -> list[str]:
    return [
        required
        for required in HIGH_PRIORITY_FILES
        if not any(Path(item.replace("\\", "/")).name == Path(required).name for item in loaded)
    ]


def check(ctx: dict) -> dict:
    status = ctx.get("status", "").strip().upper()
    loaded = [f.strip() for f in ctx.get("loaded_files", "").split(",") if f.strip()]

    if status not in ALLOWED_STATUS:
        return {
            "status": "DENIED",
            "error": "G15_INVALID_STATUS",
            "hint": f"status 必须为 {sorted(ALLOWED_STATUS)} 之一。收到: '{status}'。"
                    f"格式: gate_pass('g_knowledge', loaded_files='unity/standard/shader/shader-structure.md, unity/standard/script/script-structure.md', status='COMPLETE')",
        }

    missing = _missing(loaded)
    if missing:
        return {
            "status": "DENIED",
            "error": "G15_MISSING_HIGH_PRIORITY",
            "hint": f"Status=COMPLETE 但未声明已读: {missing}。loaded_files 示例: '{', '.join(HIGH_PRIORITY_FILES)}'",
        }

    resolved = []
    details = []
    ambiguous = []
    unresolved = []
    for entry in loaded:
        try:
            item = resolve_entry(entry, PROJECT_ROOT, KB_ROOTS)
            resolved.append(item.resolved_path.as_posix())
            details.append(item.as_dict())
        except AmbiguousKnowledgePath:
            ambiguous.append(entry)
        except KnowledgePathError:
            unresolved.append(entry)

    if ambiguous:
        return {
            "status": "DENIED",
            "error": "G15_AMBIGUOUS_FILE",
            "hint": f"以下声明条目无法唯一解析: {ambiguous}。请使用稳定 knowledge ID 或规范相对路径。",
        }
    if unresolved:
        return {
            "status": "DENIED",
            "error": "G15_UNRESOLVED_FILE",
            "hint": f"以下声明条目无法解析到真实文件: {unresolved}。知识库使用 unity/ 与 rules/ 稳定 ID。",
        }

    return {
        "status": "OK",
        "knowledge": "COMPLETE",
        "loaded": loaded,
        "resolved": resolved,
        "resolved_details": details,
    }

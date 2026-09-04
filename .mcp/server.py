"""Unity Gate MCP Server — 后果验证门禁系统 (v2).

链统一为 [g_entry, g_knowledge] — 门禁只保留「知识证据」实质校验:
  g_knowledge 校验声明条目命中真实知识文件（防编造）
  write_gated 对内容执行结构规范检查（后果验证，新增行 diff）

模式（recipe）经 gate_set_recipe 声明；脚本决策/文件分类降级为 write_gated 注解（记录不阻塞）。
Codex 对等通道: python .mcp/validation/check_norm.py <file>

工具:
  gate_set_recipe(name)              — 选择配方 (模式声明)
  gate_pass(gate_id, **context)      — 通过指定门禁
  gate_status()                       — 查看当前状态
  gate_list()                         — 列出所有门禁 + 配方
  gate_reset()                        — 重置
  script_list()                       — 列出脚本库
  write_gated(path, content, ...)     — 门禁 + 规范检查后写入
"""

import json, os, asyncio
from mcp.server import MCPServer

from gate_center import state, GATE_REGISTRY, RECIPES
from validation.script_library import list_scripts, validate_decision
from validation.project_paths import validate_path
from validation.norms import check_content, _existing_lines

server = MCPServer(name="unity-gate", version="0.5.0")


# ═══ gate_set_recipe — 选择配方 ═══

@server.tool()
async def gate_set_recipe(name: str) -> str:
    """选择配方 (Mode)。新任务第一步。

    Args:
        name: "Production" | "Research" | "Experiment" | "Debug" | "Minimal" | "Quick"
    """
    return json.dumps(state.set_recipe(name), ensure_ascii=False)


# ═══ gate_pass — 通过指定门禁 ═══

@server.tool()
async def gate_pass(gate_id: str, agent: str = "", mode: str = "", reason: str = "",
                    decision: str = "", file_type: str = "", category: str = "",
                    effect: str = "", query: str = "", summary: str = "",
                    plan_summary: str = "", loaded_files: str = "",
                    status: str = "") -> str:
    """通过指定门禁。传入门禁需要的上下文参数。

    实质门禁（配方内）:
      g_entry: agent="unity-developer"
      g_knowledge: loaded_files="shader-structure.md, script-structure.md", status="COMPLETE"
    退役门禁（调用返回 GATE_NOT_IN_RECIPE）:
      g_mode → 模式走 gate_set_recipe(name)
      g_script / g_file → 走 write_gated 注解参数

    Args:
        gate_id: 门禁 ID (如 "g_entry", "g_knowledge")
    """
    kwargs = {k: v for k, v in locals().items()
              if k not in ("gate_id",) and v}  # 跳过空值
    return json.dumps(state.pass_gate(gate_id, **kwargs), ensure_ascii=False)


# ═══ gate_status / gate_list / gate_reset ═══

@server.tool()
async def gate_status() -> str:
    """查看当前配方、门禁通过状态与写入审计."""
    return json.dumps({
        "recipe": state.recipe,
        "passed": sorted(state.passed),
        "remaining": state.remaining,
        "contexts": {k: v for k, v in state.contexts.items()},
        "writes": state.writes[-20:],   # 最近 20 条写入审计
    }, ensure_ascii=False)


@server.tool()
async def gate_list() -> str:
    """列出所有可用门禁和配方."""
    return json.dumps({
        "gates": {gid: {"name": d["name"], "requires": d["requires"],
                        "retired": d.get("retired", False)}
                  for gid, d in GATE_REGISTRY.items()},
        "recipes": RECIPES,
    }, ensure_ascii=False)


@server.tool()
async def gate_reset() -> str:
    """重置门禁状态（新任务开始）."""
    state.reset()
    return json.dumps({"status": "OK", "message": "已重置。请 gate_set_recipe(name) 开始新任务。"})


# ═══ script_list — 脚本库 ═══

@server.tool()
async def script_list() -> str:
    """列出 scripts/roslyn/ 中的所有可用脚本."""
    scripts = list_scripts()
    return json.dumps({"scripts": scripts, "count": len(scripts)}, ensure_ascii=False)


# ═══ write_gated — 带门禁写入（知识证据 + 内容后果验证）═══

@server.tool()
async def write_gated(path: str, content: str, mode: str = "", script_decision: str = "",
                      file_type: str = "", category: str = "", effect: str = "") -> str:
    """门禁校验后写入文件。配方门禁全部通过 + 内容通过规范检查后才放行。

    门禁: [g_entry, g_knowledge]（知识证据）— 所有模式（含 Quick/Minimal）一致。
    后果验证: 内容执行结构规范检查（norms.py），error 级违规阻断，warning 级放行携带提示。
    注解（非阻塞，记录审计）: mode 缺省取配方；script_decision 复用脚本库校验。

    Args:
        path: 目标文件路径（相对于项目根目录）
        content: 文件内容
        mode: 模式注解（缺省 = 当前配方）
        script_decision: 脚本决策注解，如 "USE scene-query.cs" | "NONE"
        file_type / category / effect: 文件分类注解
    """
    path_check = validate_path(path)
    if path_check["status"] != "OK":
        return json.dumps(path_check, ensure_ascii=False)

    gate_check = state.can_write()
    if gate_check["status"] != "OK":
        return json.dumps(gate_check, ensure_ascii=False)

    full_path = path_check.get("full_path", path)

    # ── 后果验证: 结构规范检查（新增行 diff）──
    existing = _existing_lines(full_path)
    norm = check_content(path, content, existing=existing)
    if norm["errors"]:
        return json.dumps({
            "status": "DENIED", "error": "NORM_VIOLATION",
            "violations": norm["errors"],
            "hint": "内容违反结构规范（来源见 violations[].source）。修正后重试，或确认规范不适用后提交 review。",
        }, ensure_ascii=False)

    # ── 注解（非阻塞，记录审计）──
    annotations: dict = {"mode": mode or state.recipe}
    if script_decision:
        annotations["script_decision"] = validate_decision(script_decision)
    if file_type:
        annotations["file_type"] = file_type
    if category:
        annotations["category"] = category
    if effect:
        annotations["effect"] = effect

    # ── 原子写: 同目录隐藏临时文件 + rename ──
    # Editor watcher 只看到完整新内容, 消除「截断+流式写入」的中间混合态
    # （瞬时编译错误 / mtime 误报属同一竞争类; 前导点文件 Unity 忽略不导入）
    tmp_path = os.path.join(os.path.dirname(full_path),
                            "." + os.path.basename(full_path) + ".uetmp")
    try:
        os.makedirs(os.path.dirname(full_path), exist_ok=True)
        with open(tmp_path, "w", encoding="utf-8") as f:
            f.write(content)
        os.replace(tmp_path, full_path)
        record = {"path": path, "bytes": len(content.encode("utf-8")),
                  "recipe": state.recipe, "annotations": annotations}
        state.writes.append(record)
        if len(state.writes) > 100:
            state.writes = state.writes[-100:]
        response = {
            "status": "OK", "written": path,
            "bytes": len(content.encode("utf-8")),
            "recipe": state.recipe, "passed": sorted(state.passed),
            "annotations": annotations,
        }
        if norm["warnings"]:
            response["warnings"] = norm["warnings"]   # 提示不阻断
        return json.dumps(response, ensure_ascii=False)
    except Exception as e:
        try:   # 清理失败/中断残留的隐藏临时文件
            if os.path.isfile(tmp_path):
                os.remove(tmp_path)
        except Exception:
            pass
        return json.dumps({"status": "ERROR", "error": str(e),
                           "hint": "文件写入失败。"})


# ═══ main ═══

async def main():
    try:
        # mcp 2.x: MCPServer 自带 stdio 入口（含初始化选项 + capabilities 自动生成）
        await server.run_stdio_async()
    except Exception as e:
        # stdio server 需要 MCP client 连接 — 直接运行会报错是正常的
        import sys
        print(f"MCP server stopped: {e}", file=sys.stderr)
        print("This server is started automatically by Claude Code via .mcp.json.", file=sys.stderr)
        print("To test: uv run python tests/test_recipes.py", file=sys.stderr)


if __name__ == "__main__":
    asyncio.run(main())

# script-decision — 脚本决策分支

> 由 [G2] 触发。判断是否需要脚本、是否已有等价脚本、如何放置。

---

## 分支树

```
[G2] 脚本决策（流程提示，非门禁 — g_script 已退役）
  │
  ├── ASK: 本次任务是否需要编写/执行 Roslyn 脚本？
  │     ├── NO  → Decision: NONE
  │     │      → 写入时经 write_gated 注解 script_decision="NONE"（记录不阻塞）
  │     │      → 进入下一步
  │     └── YES → 继续
  │
  ├── ASK: 该功能是否已在 scripts/roslyn/ 中有等价脚本？
  │     TOOLS: ls scripts/roslyn/（或 script_list）
  │     ├── YES → Decision: USE
  │     │      → 使用已有脚本，不写新的
  │     │      → 例: 查场景 → scene-query.cs
  │     │      → 例: 管线诊断 → pipeline-check.cs（原名 check_pipeline.cs）
  │     │      → 例: 整理场景 → scene-organize.cs
  │     └── NO  → 继续
  │
  └── ASK: 该脚本逻辑是否可复用于后续会话？
        ├── YES → Decision: CREATE reusable
        │      → 写入: scripts/roslyn/<name>.cs
        └── NO  → Decision: CREATE tmp
               → 写入: tmp/<name>.cs
```

---

## OUTPUT 格式

```
## G2: Script Decision
NeedScript: YES | NO
LibraryCheck: <ls scripts/roslyn/ result>
Match: <script> | NONE
Decision: USE <script> | CREATE reusable | CREATE tmp | NONE
Path: <file path if CREATE>
```

---

## 脚本库

| 脚本 | 用途 |
|------|------|
| `scene-query.cs` | 场景层级遍历（含组件 + active 状态） |
| `scene-organize.cs` | 测试物体分组整理到 __TestObjects__ |
| `pipeline-check.cs` | 渲染管线 + Quality Level 诊断 |
| `scan-temp-objects.cs` | 扫描临时物体 |

> 屏幕调试不再需要 Roslyn 脚本：固定 Feature `Assets/Mine/Scripts/Debug/DebugOutputFeature`（Inspector 配置）
> 新增可复用脚本后 → 更新上表

## Red Flags

- Decision=USE 但 agent 写了新脚本 → 废弃新脚本，使用已有
- 未 ls scripts/roslyn/ 就声称"无等价脚本" → 退回重查

# Script Templates

Reusable C# templates organized by responsibility. These entries are independent of a specific Shader feature.

Family specifics and in-project exemplars live in each subfolder README; consult it before writing a Script.

| Family | Status | Entry |
|---|---|---|
| Baker | Available | [baker/](baker/) — [baker-service.cs](baker/baker-service.cs) 一次性重烘焙服务层 |
| Window | Available | [window/](window/) — [editor-window.cs](window/editor-window.cs) 编辑器工具窗口壳(跨族共享) |
| Generator | Available | [generator/](generator/) — [generator-facade.cs](generator/generator-facade.cs) 可再生资产生成 facade |
| Manager | Available | [manager/](manager/) — [runtime-manager.cs](manager/runtime-manager.cs) 场景编排组件 |
| Controller | Available | [controller/](controller/) — [controller-processor.cs](controller/controller-processor.cs) 接口 + 实现 |

Window shells for a specific family: check `window/README.md` consumption table (Baker型 = Bake/Load/Save, Generator型 = Generate/Save).

Feature-bound code templates and postprocess guidance live under `shader/postprocess/`; function-independent doc templates live under `templates/standard/`.

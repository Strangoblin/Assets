# Unity Developer Templates

Templates are classified by actual responsibility, not by file extension.

## Standard

- [standard/](standard/) — function-independent code skeletons + documentation templates

## Script

- [script/](script/) — responsibility families: Baker / Window / Generator / Manager / Controller

## Shader Features

- [shader/](shader/) — feature families; each family directory keeps only `README.md` as markdown, the rest are code template bodies

A `.cs` file belongs under `shader/` when it is a RendererFeature, RenderPass, Volume, or other shader-feature support script; an `EditorWindow` shell belongs under `script/window/`. A standalone `.hlsl` include library shared across effects belongs under `shader/hlsl/`; a private per-effect library sits next to its shader (the `shader/render/` effect-shader + effect-function pair).

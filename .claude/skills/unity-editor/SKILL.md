---
name: unity-editor
description: Remote control Unity Editor via CLI using unityctl. Activate when user mentions Unity Editor, play mode, asset compilation, Unity console logs, C# script debugging, Unity tests, scene loading. Use for launching/stopping editor, entering/exiting play mode, compiling scripts, viewing logs, loading scenes, running tests, or executing arbitrary C# in Unity context.
---

# unityctl - Unity Editor Remote Control

Control a running Unity Editor from the command line without batch mode.

## Setup (Required First)

Run `unityctl status` first to check what's already running. If Unity is already connected, skip straight to commands.

**Platform config (Bridge + availability + troubleshooting):** [../../agents/unity-developer/AGENT.md](../../agents/unity-developer/AGENT.md)
**Full command reference:** [../../agents/unity-developer/cli/unityctl.md](../../agents/unity-developer/cli/unityctl.md)

## Verifying Changes

Pick the cheapest observation that answers the question — prefer structured tools (consume less context, are diffable, and precise). Visual quality is confirmed by a human in the Editor:

| What you need to verify | Tool |
|------------------------|------|
| Scene hierarchy, components, properties | `snapshot` (with `--components`, `--filter`) |
| UI layout, visibility, screen positions | `snapshot --screen` |
| Runtime behavior, errors, warnings | `logs` |
| Specific value or state | `script eval` (query it directly) |
| Test correctness | `test run` |

**Rule of thumb:** if you can express the expected result as a value or property, verify with `snapshot`, `logs`, or `script eval` — leave visual quality (art, shaders, layout polish) to human observation in the Editor.

## Scene Observation & Manipulation Workflow

Use `snapshot` to observe, `ui click` to interact, `eval --id` for custom actions, then `snapshot` to verify.

```bash
unityctl snapshot --screen                 # See the scene with UI screen bounds
unityctl ui click --name "StartButton"    # Find and click by name (one call)
unityctl snapshot                          # Verify the result
```

## Script Execution Quick Reference

```bash
unityctl script eval 'Application.version'
unityctl script eval --id -1290 'target.transform.position'
unityctl script eval -u UnityEngine.SceneManagement 'SceneManager.GetActiveScene().name'
unityctl script execute /tmp/MyScript.cs
unityctl script eval -t 600 -u UnityEditor 'return BuildPipeline.BuildPlayer(opts).summary.result.ToString();'
```

**Important:** Always use the Write tool to create the `.cs` file rather than shell heredocs.

## Typical Workflow

```bash
unityctl asset refresh       # Compile — check for errors
unityctl snapshot            # Verify scene state (structured, cheap)
unityctl play enter
unityctl snapshot            # Check runtime state with instance IDs
unityctl logs                # Check for errors/warnings
unityctl play exit
```

## Troubleshooting

Run `unityctl status` first to diagnose issues. Full troubleshooting table: [../../agents/unity-developer/cli/unityctl.md](../../agents/unity-developer/cli/unityctl.md)

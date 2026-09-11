---
name: unity-editor
description: Control Unity Editor from the command line and choose between unityctl for a running Editor and Unity's official Editor CLI for process-owned automation. Activate for play mode, compilation, logs, tests, scenes, builds, batch mode, or Editor command-line usage.
---

# unityctl - Unity Editor Remote Control

Control a running Unity Editor from the command line without batch mode.

## Setup (Required First)

Run `unityctl status` first to check what's already running. If Unity is already connected, skip straight to commands.

**Platform config (Bridge + availability + troubleshooting):** [../../agents/unity-developer/AGENT.md](../../agents/unity-developer/AGENT.md)
**Full command reference:** [../../agents/unity-developer/cli/unityctl.md](../../agents/unity-developer/cli/unityctl.md)

## Choosing the CLI

| Interface | Best for | Key constraint |
|-----------|----------|----------------|
| `unityctl` | Inspecting or controlling an already-running Editor | Requires its bridge in the same user host environment as Unity |
| Official Unity Editor CLI | CI, builds, cold imports, tests, and `-executeMethod` jobs that own the Editor process | Starts a separate Editor process; it does not RPC into the open Editor |
| Official `-version` | Safe installation/version probe | Prints the version without starting the Editor |

Routing rules:

- Prefer `unityctl` for status, scenes, play mode, logs, snapshots, and ad hoc queries against the open project.
- Never launch a second official Editor CLI instance against a project already open; use `unityctl` or a separate project copy.
- Discover the installed Editor path instead of hardcoding a version. For official arguments, use the [Unity Editor command-line documentation](https://docs.unity3d.com/Manual/EditorCommandLineArguments.html).

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

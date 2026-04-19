---
title: Weft Studio
shortTitle: Studio
eyebrow: Studio · Practical
order: 9
color: gold
icon: "⊞"
related:
  - studio-open-from-workspace
  - studio-read-only
  - getting-started
---
# Weft Studio

Weft Studio is a desktop GUI for inspecting and editing tabular models. It complements the `weft` CLI: the CLI deploys, the Studio explores. Same model format, same rules.

Studio is cross-platform (Windows / macOS / Linux), built on Avalonia + .NET 10, and ships independently of the CLI on its own release cadence (`weft-studio-vX.Y.Z` tags).

## What's in v0.1.1

- **Open `.bim` files** — tree-view explorer (tables · measures · partitions · roles), DAX editor with syntax highlighting, inspector pane.
- **Connect to a workspace** — sign in to a Power BI / Fabric workspace via XMLA, pick a dataset, open it as a **read-only snapshot**. See [Open from workspace](studio-open-from-workspace.md).
- **Save As `.bim`** — export any open model (file or workspace) to local JSON.
- **Read-only safety** — workspace-loaded models cannot accidentally overwrite the live server. Save is disabled; an orange banner makes the state obvious. See [Read-only snapshot semantics](studio-read-only.md).

## What it doesn't do (yet)

- **Deploy back to a workspace.** Studio is read-only against live servers in v0.1.x. To push edits back, Save As `.bim` and use `weft deploy`.
- **Diagram view.** The visual model layout is on the v0.2 roadmap.
- **Diff / compare.** A diff mode is on the v0.3 roadmap.
- **Service Principal auth in the GUI.** SP modes (cert-store / cert-file / secret) stay CLI-only — Studio is for developers, automation lives in `weft.yaml`.

## Install

Studio releases are published as platform binaries on [GitHub Releases](https://github.com/movmarcos/weft/releases). Look for the `weft-studio-v*` tags.

To build from source:

```bash
git clone https://github.com/movmarcos/weft.git
cd weft
dotnet run --project studio/src/WeftStudio.Ui
```

Requires the .NET 10 SDK.

## First-run configuration

Two things to know on first launch:

1. **No baked ClientId in v0.1.1.** Connect-to-workspace needs an AAD app registration's ClientId. Provide one of:
   - Environment variable `WEFT_STUDIO_CLIENTID`
   - User override in `settings.json` (set via the dialog's Advanced…)
   - `--client-id <guid>` command-line argument

   Without a ClientId the Connect dialog still opens but Sign in is disabled. Open `.bim` works regardless.

   v0.1.2 will register a public multi-tenant AAD app and bake the ClientId so first-run needs no configuration.

2. **MSAL token cache** lives at `<LocalAppData>/WeftStudio/msal.cache`. Sign-ins persist across restarts; clear that file to force a fresh sign-in.

## Architecture at a glance

```
ShellWindow (Avalonia)
    │
    ├── ExplorerViewModel ── tree of tables / measures / partitions / roles
    ├── DaxEditorViewModel ── per-tab measure editor (AvaloniaEdit + Prism)
    └── InspectorViewModel ── selected-object properties

ConnectDialog (Avalonia)
    │
    └── ConnectDialogViewModel ── state machine over Idle → Ready → SigningIn
                                                     → Fetching → Picker → Loading
            │
            └── IConnectionManager ── orchestrates SignInAsync (Weft.Auth)
                                                  · ListDatasetsAsync (Weft.Xmla)
                                                  · FetchModelAsync   (Weft.Xmla)
```

The UI never touches TOM directly — the layering goes UI → VM → `IConnectionManager` → `Weft.Xmla` / `Weft.Auth`. Same `Weft.Auth` and `Weft.Xmla` libraries the CLI uses.

## Where Studio fits in the workflow

Studio and the CLI are designed to overlap, not compete:

| Task | Use the CLI | Use Studio |
|---|---|---|
| Inspect a model on disk | `weft validate` | Open `.bim` |
| Browse a live workspace | — (not yet) | Connect to workspace |
| Edit a measure | text editor | DAX editor with highlighting |
| Deploy to dev / prod | `weft deploy` | Save As `.bim`, then CLI |
| CI pipelines | always the CLI | — |

Studio is for exploration and authoring. The CLI is for reliable, scriptable deployment.

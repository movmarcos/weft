# Weft Studio — Design Spec

**Date:** 2026-04-18
**Status:** Draft — awaiting approval before implementation plan
**Working name:** Weft Studio (subject to rename)

## 1. Summary

Weft Studio is a cross-platform desktop application that edits, visualizes, diffs, and deploys Power BI / Fabric semantic models. It is positioned as a free, open-source replacement for **Tabular Editor 2** and **ALM Toolkit**, and delivers the model-visualization feature that Tabular Editor 3 sells as a paid feature. It sits alongside the existing `weft` CLI; both share `Weft.Core` as their engine, so every deploy the GUI performs is identical to what CI/CD pipelines already execute.

## 2. Goals and non-goals

### Goals (v1.0)
- Edit measures, columns, descriptions, display folders, and formatting in a `.bim` or Tabular Editor folder model.
- Visualize the model schema (tables, columns, relationships) on a diagram canvas.
- Trace a measure's dependencies: click → highlight the tables and columns it uses, dim the rest, color traversed relationships.
- Diff a working model against another `.bim` file or a live Power BI workspace.
- Deploy via the existing `Weft.Core` engine — surgical TMSL, partition preservation, history-loss gate surfaced up front.
- Ship on Windows, macOS, Linux from one codebase.
- Leave a clean plugin boundary so AI agents can attach in v1.5 without a rewrite.

### Non-goals (explicit)
- No C# script host (a major TE2 feature).
- No Best Practice Analyzer in v1 (v2 target).
- No perspectives, translations, RLS/roles, or calculation-group editors in v1 (v2 target).
- No full DAX IntelliSense — syntax highlighting and a formatter only.
- No shipped AI agents in v1 — only the plugin surface design. AI agents land in v1.5.
- No multi-model workspaces, no split-window model viewing. One model open at a time.
- No "live-edit" against a workspace. Server changes only happen via explicit deploy.

## 3. Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Weft Studio (Avalonia app)               │
│  ┌───────────────────────────────────────────────────────┐  │
│  │ UI layer (Avalonia + ReactiveUI)                      │  │
│  │  Shell · Explorer · Diagram · Diff · Search · DAX ed  │  │
│  └───────────┬───────────────────────────────────────────┘  │
│              │  MVVM bindings (views never touch TOM)        │
│  ┌───────────▼───────────────────────────────────────────┐  │
│  │ Application layer (WeftStudio.App)                    │  │
│  │  ModelSession · DiagramLayout · DependencyTracer      │  │
│  │  DiffPresenter · ConnectionManager · PluginHost(v1.5) │  │
│  └───────────┬───────────────────────────────────────────┘  │
│              │                                               │
│  ┌───────────▼───────────────────────────────────────────┐  │
│  │ Weft.Core (NuGet — shared with CLI)                   │  │
│  │  loaders · diff · tmsl · partitions · params · hooks  │  │
│  └───────────┬───────────────────────────────────────────┘  │
│              │                                               │
│  ┌───────────▼───────────────────────────────────────────┐  │
│  │ Weft.Auth · Weft.Xmla (NuGet — shared with CLI)       │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

**Key invariant:** the UI layer never touches TOM directly. All model access goes through `ModelSession`. This keeps the GUI and CLI behaviorally identical, and makes ViewModels testable without an Avalonia window.

**Repo strategy:** `movmarcos/weft-studio` — a new sibling repo to `movmarcos/weft`, consuming `Weft.Core`, `Weft.Auth`, `Weft.Xmla`, `Weft.Config` as published NuGet packages. Separate repo chosen because release cadence differs, CI-only users should not pull a desktop app installer, and weft's CI stays lean.

**Consequence for `weft` (existing repo):** the NuGet publish step for `Weft.Core` is already wired (`release-artifacts.yml`). Similar packaging must be added for `Weft.Auth`, `Weft.Xmla`, and `Weft.Config`. `InternalsVisibleTo("WeftStudio.App")` may be needed for types the CLI keeps private but Studio needs.

## 4. Components

| Layer | Component | Responsibility |
|---|---|---|
| UI | `WeftStudio.Shell` | Main window, activity bar, tab region, status bar |
| UI | `Explorer` | Model tree (tables → columns / measures / partitions / relationships) |
| UI | `DiagramCanvas` | Schema ERD + dependency overlay, zoom/pan, auto-layout with manual override |
| UI | `DiffDashboard` | Change-summary card, history-loss verdict, partition-preservation count, deploy CTA, drill-in side-by-side |
| UI | `DaxEditor` | AvaloniaEdit + custom DAX grammar, syntax highlight, format-on-save, brace matching |
| UI | `SearchPane` | Full-text search over measure names, DAX bodies, column descriptions |
| UI | `Inspector` | Context-sensitive properties for the selected object |
| App | `ModelSession` | Wraps TOM `Database`; owns command history; only write path to the model |
| App | `ModelCommand` (abstract) | Typed commands (`RenameMeasureCommand`, `UpdateDaxCommand`, etc.) with `Apply` + `Revert` + description |
| App | `DiagramLayout` | Computes initial table positions; respects user overrides stored in a sidecar |
| App | `DependencyTracer` | Given a measure, returns the set of tables/columns used and relationship edges traversed |
| App | `DiffPresenter` | Wraps `Weft.Core`'s `ChangeSet` with presentation-oriented aggregates (counts by category, verdict summaries) |
| App | `ConnectionManager` | Owns authenticated workspace sessions; reuses `Weft.Auth` directly |
| App | `PluginHost` (v1.5) | MCP client; spawns agents as child processes; routes model queries to them |

## 5. Key UX flows

### 5.1 Open a model
`File → Open .bim` (native file picker) or `File → Connect to workspace` (MSAL via `Weft.Auth`, then workspace picker, then dataset picker). `ConnectionManager` returns a populated TOM `Database`; `ModelSession` takes ownership; the Explorer tree populates.

### 5.2 Edit a measure and deploy
Click measure in Explorer → opens a tab in `DaxEditor`. User edits, saves (`Ctrl/Cmd+S`) — `UpdateDaxCommand` applies, `ModelSession` writes back to the originating `.bim` (or TE folder). User switches activity bar to **Diff**, target defaults to last-used workspace. `DiffDashboard` renders the summary. User clicks **Deploy** → `Weft.Core.Plan()` → `Weft.Xmla.XmlaExecutor.Execute()`. Status bar streams progress; on success, the dashboard refreshes to show zero pending changes.

### 5.3 Trace a measure's dependencies
User clicks a measure anywhere (tree, open tab, Inspector). Pressing **F12** (or the Inspector's "Trace" button) switches activity bar to **Diagram**. `DependencyTracer` computes the used-by set; canvas enters trace mode: highlighted tables, dimmed unused tables, colored relationship edges per the agreed mockup. **Esc** clears the trace.

### 5.4 Visualize (no measure selected)
Diagram mode with no selection renders the plain schema ERD: tables as boxes, relationships as lines with cardinality markers. Pan/zoom/manual reposition. Positions persist in a sidecar `.layout.json` next to the `.bim` so layout is preserved across sessions.

## 6. Data flow & persistence

**Source of truth:** `ModelSession` owns one TOM `Database` and a `ChangeTracker` (stack of `ModelCommand`). Commands are the only write path.

**Undo/redo:** command-based, not property-observer diff. Every mutation is a named, reversible command with a human-readable description. The Undo menu shows the command list by description.

**Dirty tracking:** `ModelSession.IsDirty` is true whenever `ChangeTracker` has uncommitted commands. Save resets.

**Save format:** mirrors load format.
- Opened from `.bim` → save writes `.bim` (TOM's `JsonSerializer`, pretty-printed).
- Opened from TE folder → save writes the folder layout.
- Opened from a live workspace (no local file) → **Save** is disabled. The user must `Save As .bim` first, or switch to Diff mode and deploy (writes to the server, not to disk).

**Config interop:** if the user opens a `.bim` that has a sibling `weft.yaml`, Studio offers to use the file's profiles as deploy targets. Studio never edits `weft.yaml`.

## 7. Live workspace connections

- `ConnectionManager` reuses `Weft.Auth` directly. Five modes (Interactive, DeviceCode, SPCertStore, SPCertFile, SPSecret) are identical to the CLI.
- MSAL tokens cache to `%LOCALAPPDATA%/WeftStudio/msal.cache` (Windows), `~/Library/Application Support/WeftStudio/msal.cache` (macOS), `~/.local/share/weft-studio/msal.cache` (Linux). No bespoke secret storage.
- Downloading a model from XMLA uses `Weft.Xmla.TargetReader`. Streaming with progress reporting to the status bar.
- **Snapshot semantics:** a loaded workspace model is a read-only snapshot from the moment of fetch. The server can diverge; Studio never auto-refreshes. Re-fetching is an explicit user action that prompts if there are unsaved edits.

## 8. Testing strategy

| Tier | Frameworks | Coverage |
|---|---|---|
| Unit | xUnit + FluentAssertions | Every `ModelCommand`, `DependencyTracer`, `DiffPresenter`, `ConnectionManager` logic |
| ViewModel | xUnit + `Avalonia.Headless.XUnit` | VMs exercised without a real window — open model, apply command, assert Inspector state |
| Integration | xUnit, gated by `WEFT_INT_*` env vars | Live-workspace fetch / edit / diff / deploy, matching `Weft.Integration.Tests` convention |
| Visual (deferred) | Avalonia screenshot tests | Diagram canvas regression. Deferred to v1.5 — known flaky across OS versions |

## 9. Packaging & distribution

| OS | Primary | Secondary |
|---|---|---|
| Windows | MSIX installer (code-signed, post-v1) | Portable ZIP |
| macOS | `.app` bundle + `.dmg` (unsigned v1; notarization post-v1) | — |
| Linux | AppImage | Tarball; Flatpak as stretch |

- Release artifacts built via GitHub Actions matrix, mirroring `weft`'s `release-artifacts.yml`.
- Installer size target: ≤ 40 MB on Windows (self-contained .NET 10 runtime, trimmed).
- v1.0: "new version available" banner linking to the release page. v1.5: in-app auto-update via Velopack (or NetSparkle).
- Distribution surfaces v1.0: GitHub Releases + Homebrew (macOS/Linux). Microsoft Store post-v1.

## 10. Error handling

- Every user-initiated action is wrapped by a single `ExecuteAsync<T>(Func<Task<T>>)` boundary that catches, logs to the Log pane, and surfaces a `Notification` banner.
- Domain exceptions from `Weft.Core` (`PartitionIntegrityException`, `ParameterApplicationException`, history-loss gate violations) map to specific banner copy — reuse the exception message, don't `.ToString()` the stack.
- Unhandled exceptions write a crash file to `%LOCALAPPDATA%/WeftStudio/crash/` (or XDG equivalent) and prompt the user to open a GitHub issue with the log attached.
- No telemetry in v1 without explicit opt-in. Crash reports are local-only unless the user manually attaches them.

## 11. Plugin / AI agent surface (designed v1, shipped v1.5)

- Protocol: **Model Context Protocol (MCP)**. Studio is the MCP *client*; agents are MCP *servers* running as child processes.
- Agent capabilities exposed: read-only model introspection (tables, measures, columns, DAX bodies, relationships) and advisory tools (agents can suggest changes as `ProposedCommand` objects but not apply them).
- Agent-proposed commands appear in a "Suggestions" pane. The user reviews and accepts. Accepting runs the command through the same `ModelSession` write path — no plugin shortcut.
- Agent configuration: `%APPDATA%/WeftStudio/agents.json` enumerates executable paths + argv + env. First-class built-ins bundled with Studio (quality checker, naming-convention checker) ship in v1.5 alongside the surface.
- v1 deliverable: clean `WeftStudio.App` boundaries so `PluginHost` can drop in without reshaping core types. Interfaces drafted and exported but not wired to a live MCP client.

## 12. Roadmap

| Milestone | Scope | Est. |
|---|---|---|
| **v0.1** | Open `.bim`, Explorer tree, Inspector, DAX editor (no visualizer, no deploy) | 6 wk |
| **v0.2** | Diagram mode (schema ERD, no overlay), live workspace read | +4 wk |
| **v0.3** | Diff mode + deploy (summary dashboard), file↔file and file↔workspace | +4 wk |
| **v1.0** | Dependency overlay, Search mode, polish, installers for 3 OSes | +6 wk |
| **v1.5** | MCP plugin host, first AI quality-review agent, auto-update | +2 mo |
| **v2.0** | Best Practice Analyzer, perspectives, translations, RLS editor, calculation groups | later |

Total v1.0 ≈ 5 months of focused work by one developer; several milestones parallelizable.

## 13. Open questions / deferred

- **Naming:** "Weft Studio" is a working title. Brainstorm alternatives before v0.1 if desired.
- **TE folder save format:** confirm which TE folder layout version to target (TE2 folder vs TE3 folder — they differ).
- **Diagram sidecar file:** `.layout.json` next to the `.bim`? Or embedded in `%APPDATA%` keyed by model hash? Chose the sidecar form for portability; revisit if users dislike the extra file.
- **DAX formatter library:** evaluate `Microsoft.AnalysisServices.Tabular` built-in formatter vs the community `DaxFormatter` HTTP service vs a local port. Decide before v0.1.
- **Homebrew tap:** reuse `marcosmagri/tap` (already referenced in weft docs) or create a new `weft-studio` tap. Minor.

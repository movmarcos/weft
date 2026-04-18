# Weft Studio v0.1.1 — Open from Workspace · Design Spec

**Date:** 2026-04-18
**Status:** Draft — awaiting approval before implementation plan
**Parent spec:** `2026-04-18-weft-studio-design.md`
**Supersedes in:** Sections §5.1 and §7 of the parent spec (implements what was previously scheduled for v0.2)

## 1. Summary

Adds a second entry point to Weft Studio alongside **File → Open .bim**: **File → Connect to workspace…**. Users paste an XMLA endpoint URL (e.g. `powerbi://api.powerbi.com/v1.0/myorg/<workspace>`), sign in via MSAL, pick a dataset from a sortable + searchable grid, and the model loads as a **read-only snapshot** into the same shell the `.bim` flow uses.

This unblocks users who work against live Fabric / Power BI workspaces instead of `.bim` files on disk — which is most real-world Power BI development. v0.1.0 without this feature is effectively file-editor-only; v0.1.1 makes Studio usable for people who live in the service.

Ship target: **v0.1.1** — a patch release on top of v0.1.0. Diagram work stays in v0.2.

## 2. Goals and non-goals

### Goals (v0.1.1)
- **File → Connect to workspace…** modal dialog with paste-URL + sign-in + dataset grid.
- Support **Interactive** and **DeviceCode** MSAL auth modes (the two relevant to a GUI).
- Sortable + searchable dataset grid (Avalonia `DataGrid`) scaling to 100+ datasets per workspace.
- **Read-only snapshot** semantics: Save menu disabled, orange banner across the shell, **Save As .bim** available.
- **Reload from workspace** menu item for refetching.
- Recent-workspaces dropdown in the URL field, persisted in the existing `settings.json`.

### Non-goals (explicit)
- **Save back to server.** Workspace-loaded models are read-only. To persist edits: Save As `.bim`, then the v0.3 deploy flow pushes them back via `weft deploy`.
- **Baked default ClientId.** v0.1.1 reads ClientId from `--client-id` arg / env var / user override / empty default. v0.1.2 will register a public multi-tenant AAD app and bake the resulting ClientId so first-run needs no configuration.
- **Service Principal auth modes in the GUI** (CertStore / CertFile / Secret). Those remain CLI-only — the GUI is for developers, ops automation lives in `weft.yaml`.
- **Multi-workspace session.** One workspace at a time, matching v0.1.0's one-model invariant.
- **Live-edit / auto-refresh.** Snapshot semantics from parent spec §7 preserved — re-fetch is an explicit user action.

## 3. Architecture

```
studio/src/WeftStudio.App/
  Connections/                    ← new folder
    ConnectionManager.cs          orchestrator — sign in → list → fetch
    WorkspaceReference.cs         parsed XMLA URL (server + workspace name)
    DatasetInfo.cs                grid row record (Name, Size, Updated, ...)
    WorkspaceUrlException.cs      malformed URL signal
  AppSettings/
    ClientIdProvider.cs           precedence resolver for ClientId
  ModelSession.cs                 modified — adds ReadOnly property
  Settings/                       modified — adds RecentWorkspaces list

studio/src/WeftStudio.Ui/
  Connect/                        ← new folder
    ConnectDialog.axaml(.cs)      the modal
    ConnectDialogViewModel.cs     state machine driving the dialog
    DatasetRow.cs                 VM row mirroring DatasetInfo
  Shell/
    ShellWindow.axaml             modified — File → Connect menu item, read-only banner
    ShellViewModel.cs             modified — OpenWorkspaceCommand, IsReadOnly, WorkspaceLabel

src/Weft.Auth/                    minor change
  AuthOptions.cs                  TenantId becomes optional; empty = /common authority
```

Layering invariant preserved: the UI never touches TOM. The dialog VM drives `ConnectionManager`; `ConnectionManager` returns a ready `ModelSession`; `ShellViewModel.Explorer` is assigned, same as an `.bim` open. All the tree-building / tab-opening / inspector logic from v0.1.0 applies unchanged.

**Dependencies added:**
- `Avalonia.Controls.DataGrid` — one `PackageReference` in `WeftStudio.Ui.csproj`.

Nothing else new. `Weft.Auth` and `Weft.Xmla` already ship the required APIs.

## 4. Components

| Layer | Component | Responsibility |
|---|---|---|
| App | `ConnectionManager` | 3 async methods: `SignInAsync`, `ListDatasetsAsync`, `FetchModelAsync`. Stateless — all intermediate values (token, workspace ref) live on the dialog VM |
| App | `WorkspaceReference` | Parses / validates XMLA URL. Exposes `Server` (full URL) and `WorkspaceName` (last path segment for Fabric URLs, empty otherwise). Throws `WorkspaceUrlException` on malformed input |
| App | `DatasetInfo` | Record: `Name`, `SizeBytes`, `LastUpdatedUtc`, `RefreshPolicy` ("incremental" / "full" / null), `Owner`. Populated from XMLA `SELECT …` against `$SYSTEM.TMSCHEMA_MODELS` etc. |
| App | `ClientIdProvider` | Resolves ClientId via precedence: `--client-id` arg → `WEFT_STUDIO_CLIENTID` env → `settings.json` user override → baked default (empty for v0.1.1) |
| App | `ModelSession.ReadOnly` | New property. `true` when loaded from workspace, `false` for `.bim` |
| App | `Settings.RecentWorkspaces` | New list of `RecentWorkspace(WorkspaceUrl, LastDatasetName, AuthMode, LastUsedUtc)`; capped at 10, most-recent first |
| UI | `ConnectDialog` | Avalonia `Window`, modal over `ShellWindow`. States: Idle, SigningIn, Fetching, Picker, Loading. Cancel closes. |
| UI | `ConnectDialogViewModel` | ReactiveUI state machine. Exposes: `Url`, `AuthMode`, `IsSignedIn`, `IsBusy`, `ErrorBanner`, `Datasets` (`ObservableCollection<DatasetRow>`), `FilterText`, `SelectedRow` |
| UI | `DatasetRow` | VM wrapper around `DatasetInfo` suitable for `DataGrid` binding |
| UI | `ShellViewModel.IsReadOnly` | Drives banner visibility and `SaveCommand.CanExecute` |
| Auth | `AuthOptions` | `TenantId` becomes nullable/optional. When null or `"common"`, `InteractiveAuth` / `DeviceCodeAuth` use the `/common` authority (MSAL auto-detects tenant from user's browser login) |

## 5. UX flow

### 5.1 Entry
**File → Connect to workspace…** (between **Open…** and the **Separator**). Keyboard shortcut `Ctrl/Cmd+Shift+O`.

### 5.2 Dialog state progression

```
Idle ──valid URL──► Ready ──Sign in──► SigningIn ──ok──► Fetching ──ok──► Picker
                      │                    │                 │                │
                      │                auth fail         xmla fail       Open clicked
                      │                    │                 │                │
                      ▼                    ▼                 ▼                ▼
                  [banner]             [banner]          [banner]        Downloading
                                                                              │
                                                                    fetch ok  │  fetch fail
                                                                              ▼
                                                                       [close, shell loaded]
```

State visible in the dialog:
- **Idle/Ready:** URL textbox, auth-mode radio (Interactive / DeviceCode), Advanced button, ClientId warning if empty.
- **SigningIn:** spinner on button, Cancel enabled.
- **Fetching:** "Loading datasets…" indicator over the grid area.
- **Picker:** dataset grid (sortable columns: Name · Size · Updated · Refresh policy · Owner), name-only search box, row count indicator, default sort Updated ↓.
- **Loading:** progress bar during model download.

Each async step is `CancellationToken`-aware; Cancel aborts whatever's in flight.

### 5.3 After load
- Dialog closes.
- `ShellViewModel.Explorer` set (tree populates exactly like an `.bim` open).
- `ShellViewModel.IsReadOnly = true` → orange banner renders at top of window: **◉ READ-ONLY snapshot of `<workspace> / <dataset>` · fetched `<time>` · reload to refresh · server changes won't auto-sync**.
- `File → Save` disabled (`CanExecute` returns false).
- `File → Save As .bim…` enabled (new menu item in v0.1.1).
- `File → Reload from workspace` enabled — re-runs the fetch for the current workspace + dataset, prompting if the user has unsaved edits.
- Status bar: `{workspace} / {dataset} · read-only snapshot · fetched HH:mm`.

### 5.4 Recent workspaces dropdown
The URL textbox shows a dropdown with up to 10 recent workspace URLs, most-recent first. Picking one pre-fills URL and auth-mode. No stored credentials — silent re-auth via the MSAL token cache.

## 6. Data, persistence, ClientId

### 6.1 ClientId resolution (precedence)
1. `--client-id <guid>` command-line argument (dev / CI).
2. `WEFT_STUDIO_CLIENTID` environment variable.
3. User override in `%APPDATA%/WeftStudio/settings.json` (`Settings.ClientIdOverride`), set via the dialog's **Advanced…** button.
4. Baked default compiled into the app. **v0.1.1 ships with an empty string.** v0.1.2 will register a public multi-tenant AAD app and populate this.

If all four are empty, the dialog shows the warning banner and disables the Sign in button. Open .bim remains usable.

### 6.2 Tenant discovery
`AuthOptions.TenantId` becomes nullable. When null or `"common"`:
- MSAL `PublicClientApplicationBuilder.WithAuthority("https://login.microsoftonline.com/common")` is used.
- Interactive / DeviceCode flows auto-detect the tenant from the user's browser login.

CLI behavior preserved: CLI callers always pass an explicit `TenantId` from `weft.yaml`, so no existing config or test changes.

### 6.3 Token cache
- Location: `SpecialFolder.LocalApplicationData/WeftStudio/msal.cache` (resolves correctly per-OS via `Environment.GetFolderPath`).
- MSAL's built-in `TokenCache` JSON serialization; no bespoke crypto.
- Cache key includes ClientId + AuthMode so multiple configurations coexist without collision.

### 6.4 Recent workspaces — Settings schema
Existing `Settings` class gains a second list:

```csharp
public sealed class Settings
{
    public List<string> RecentFiles { get; set; } = new();
    public List<RecentWorkspace> RecentWorkspaces { get; set; } = new();
    public string? ClientIdOverride { get; set; }   // new
}

public sealed record RecentWorkspace(
    string WorkspaceUrl,
    string LastDatasetName,
    string AuthMode,              // "Interactive" | "DeviceCode"
    DateTime LastUsedUtc);
```

JSON-backed, written atomically via `File.WriteAllText` — same pattern as v0.1.0's `SettingsStore`. Corruption-tolerant (hardened in v0.1.0 via the Task 24 fix).

### 6.5 Model fetch
`Weft.Xmla.TargetReader.ReadDatabaseAsync(server, databaseName, accessToken, ct)` is the existing method — returns a TOM `Database`. `ConnectionManager.FetchModelAsync` wraps that + constructs:

```csharp
new ModelSession(database, sourcePath: null, readOnly: true);
```

The new `ReadOnly` property defaults to `false` for the existing `.bim`-opening constructor; workspace-opening passes `true`.

## 7. Error handling

| Trigger | Exception type | Surface | User-visible text |
|---|---|---|---|
| Malformed URL | `WorkspaceUrlException` | Inline under textbox (synchronous) | `"Must start with powerbi:// or asazure://"` |
| ClientId absent | N/A — button disabled | Warning banner in dialog | `"No ClientId configured. Open Advanced to provide one."` |
| User cancels | `OperationCanceledException` | Silent return to Idle | (none) |
| MSAL failure | `MsalException` | Dialog error banner, retry | Exception `.Message` verbatim (e.g., `"AADSTS50020: user does not exist in tenant"`) |
| Network timeout | `HttpRequestException` / `TaskCanceledException` | Dialog error banner | `"Network timeout. Check connectivity and try again."` |
| No XMLA permission | `AdomdException` (specific code) | Dialog error banner | `"This account doesn't have XMLA access to {workspace}."` |
| Empty dataset list | — (not an error) | Empty-state row in grid | `"No datasets visible. Verify XMLA read permissions on this workspace."` |
| Fetch fails mid-download | `AdomdException` | Dialog error banner; dataset stays selected | `.Message` + retry |

`ConnectionManager` does not catch; exceptions propagate. The dialog VM's try/catch maps exception → banner text. Each uncaught exception is also logged to the app log pane (v0.1.0 `ExecuteAsync<T>` wrapper) with full stack trace. Unhandled exceptions still hit v0.1.0's crash handler.

## 8. Testing strategy

| Tier | Framework | Coverage |
|---|---|---|
| Unit (App) | xUnit + NSubstitute + FluentAssertions | `ConnectionManager` with mocked `IAuthProvider` + `ITargetReader` (canned success + each exception path); `WorkspaceReference.Parse` valid + invalid; `ClientIdProvider` precedence; `ModelSession` `ReadOnly` flag |
| ViewModel (UI) | `Avalonia.Headless.XUnit` | `ConnectDialogViewModel` state machine transitions; error banners populate on exception; `DatasetRow` filter via `FilterText`; `ShellViewModel.IsReadOnly` disables `SaveCommand` |
| Integration (opt-in) | xUnit, gated on `WEFT_STUDIO_INT_TENANT`, `WEFT_STUDIO_INT_CLIENTID`, `WEFT_STUDIO_INT_WORKSPACE`, `WEFT_STUDIO_INT_DATASET` | Live AAD sign-in (Interactive or DeviceCode in CI), real dataset list, real model fetch. Skipped by default |

**Test fixtures added:**
- `FakeAuthProvider` → returns canned `AccessToken` or throws a specific exception on demand.
- `FakeConnectionReader` → returns canned `List<DatasetInfo>` / canned `Database`.
- `workspace-fixtures.json` → two fixtures: 3 datasets (normal) and 0 datasets (empty-state).

**Target counts** (approximate):
- ~10 new App-layer tests.
- ~10 new ViewModel tests.
- Integration tests skipped by default, gated on env vars.

Total suite after v0.1.1 ≈ **55 tests** (vs 31 at v0.1.0).

## 9. Roadmap impact

| Version | Original scope (parent spec §12) | Revised |
|---|---|---|
| **v0.1.1** | — | Open from workspace (this spec) |
| **v0.2** | Diagram mode + live workspace read | Diagram mode only (live-read moved to v0.1.1) |
| **v0.3** | Diff + deploy | unchanged |
| **v1.0** | Dependency overlay, Search mode, polish, installer | unchanged |

Net effect: v0.2 gets a little lighter, v1.0 unchanged. Estimated v0.1.1 effort: **~2–3 weeks** of focused work, ~12–16 tasks.

## 10. Open questions / deferred

- **Baked ClientId** — requires AAD app registration. Deferred to v0.1.2 once the registration exists. v0.1.1 reads from override/env/arg.
- **Owner / Storage mode / Last refresh status columns** — included in the grid design but populated only when XMLA surfaces them easily. Fall back to blank cells rather than blocking the release.
- **Save As .bim for workspace-opened sessions** — mentioned in flow §5.3. Mechanically just a File dialog + `BimSaver.SaveAs(session, path)` new method. Assumed in scope; if it becomes tricky we defer to v0.1.2.
- **Multi-field search** (owner / refresh policy) — name-only in v0.1.1. Reconsider after usage feedback.
- **Concurrent edits against the same workspace** — not protected in v0.1.1. Snapshot semantics mean the user sees their fetched state until they Reload; no detection of server-side changes between snapshot and Save As.

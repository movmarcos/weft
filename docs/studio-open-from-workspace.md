---
title: Open from workspace (Studio)
shortTitle: Workspace open
eyebrow: Studio · Practical
order: 10
color: gold
icon: "⌬"
related:
  - studio
  - studio-read-only
  - authentication
---
# Open from workspace

Studio v0.1.1 adds **File → Connect to workspace…** alongside the existing **File → Open .bim**. You paste an XMLA endpoint URL, sign in via MSAL, pick a dataset, and the model loads as a read-only snapshot in the same shell as the `.bim` flow.

Keyboard shortcut: **Ctrl/Cmd + Shift + O**.

## Prerequisites

- A **Power BI Premium** capacity (PPU, Premium per capacity, or Fabric) with the XMLA endpoint enabled.
- An **AAD app registration** with at least XMLA Read access on the workspace. The same one you use with the CLI works fine.
- The app registration's **ClientId**, supplied via `WEFT_STUDIO_CLIENTID`, `settings.json`, or `--client-id`. See [Studio overview](studio.md#first-run-configuration).

## The flow

1. **File → Connect to workspace…** — opens the Connect dialog.
2. **Paste the XMLA endpoint URL.** It must start with `powerbi://` or `asazure://`. Format:
   ```
   powerbi://api.powerbi.com/v1.0/myorg/<workspace>
   ```
   The recent-workspaces dropdown auto-completes from your last 10 sign-ins.
3. **Pick a sign-in mode.**
   - **Interactive** — opens the system browser. Best for daily use.
   - **Device code** — shows a code to enter at `microsoft.com/devicelogin`. Best for headless / SSH / VS Code Remote.
4. **Sign in & list datasets.** Studio calls MSAL → fetches the workspace's databases via XMLA → fills the dataset grid.
5. **Pick a dataset.** Filter by name in the search box; click a row to select it.
6. **Open read-only.** Studio fetches the full model definition (`SerializeDatabase` over XMLA) and loads it into the shell.

The dialog state machine is `Idle → Ready → SigningIn → Fetching → Picker → Loading`. Any failure rewinds to the previous stable state with the MSAL or ADOMD error in a banner.

## Tenant discovery

`AuthOptions.TenantId` is left empty by the dialog — MSAL uses the `/common` authority and auto-detects the tenant from your browser sign-in. You don't need to type a tenant guid into Studio.

The CLI still requires an explicit `TenantId` in `weft.yaml`; this looser default applies only to the GUI's interactive flows.

## Recent workspaces

Successful opens are remembered in `settings.json`'s `RecentWorkspaces` list (workspace URL · last dataset · auth mode · timestamp), capped at 10, most-recent first. The dropdown filters by substring as you type.

There are no stored credentials — the MSAL token cache handles silent re-auth. Closing the dialog without opening a dataset doesn't add to the list.

## What you see in the dataset grid

| Column | v0.1.1 |
|---|---|
| Name | Always populated. |
| Size | `-` (deferred to v0.1.2 — needs DMV / REST). |
| Updated | `-` (deferred to v0.1.2). |
| Refresh policy | `-` (deferred to v0.1.2). |
| Owner | `-` (deferred to v0.1.2). |

Sorting and the filter box work on every column today — so when v0.1.2 enriches the cells, no UX changes.

## After load

The shell looks like a normal `.bim` open with three additions: an orange read-only banner across the top, the Save menu disabled, and the status bar showing `<workspace> / <dataset> · read-only snapshot`. See [Read-only snapshot semantics](studio-read-only.md) for what that means.

## Common errors

| Banner text | Cause |
|---|---|
| `Must start with powerbi:// or asazure://` | Malformed URL — paste the XMLA endpoint, not the workspace web URL. |
| `AADSTS50020: user does not exist in tenant` | You signed in to a tenant that doesn't host the workspace. Sign out, retry. |
| `This account doesn't have XMLA access to <workspace>` | The signed-in user lacks XMLA Read. Ask the workspace admin. |
| `Network timeout. Check connectivity and try again.` | XMLA endpoint unreachable — VPN / firewall. |

Every uncaught exception also logs to the app log pane with the full stack trace.

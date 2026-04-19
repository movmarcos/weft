# Weft Studio v0.1.1 — Pre-built binaries

This folder ships a self-contained Windows x64 build for users on networks that block downloads from GitHub Releases. The zip is in the repo so it arrives via `git clone`.

> **Why is this in the repo?** Pragmatic workaround for blocked corporate networks. The "right" channel for binaries is GitHub Releases — see https://github.com/movmarcos/weft/releases. If you can reach Releases, prefer that path.

## Available builds

| File | Platform | Size | Self-contained? |
|---|---|---|---|
| `WeftStudio-v0.1.1-win-x64.zip` | Windows x64 | 53 MB | Yes — no .NET install required |

macOS and Linux: build from source per the [Studio install docs](https://movmarcos.github.io/weft/docs/studio.html#install).

## Install (Windows x64)

```powershell
git clone https://github.com/movmarcos/weft.git
cd weft\releases\v0.1.1
Expand-Archive WeftStudio-v0.1.1-win-x64.zip -DestinationPath C:\WeftStudio -Force
C:\WeftStudio\WeftStudio.Ui.exe
```

If Windows SmartScreen says "Windows protected your PC", click **More info → Run anyway**. The build is unsigned (open source, MIT licensed).

## File → Open .bim

Works out of the box. No setup needed.

## File → Connect to workspace (XMLA sign-in)

The Sign-in button stays disabled until you set an AAD ClientId. v0.1.1 doesn't ship one baked in. Easiest path: borrow Power BI Desktop's well-known public ClientId — it's a Microsoft first-party app pre-approved in most tenants, used by Tabular Editor, ALM Toolkit, and other community tools.

**Set the env var (works in CMD or PowerShell, one-time, persists across reboots):**

```
setx WEFT_STUDIO_CLIENTID 872cd9fa-d31f-45e0-9eab-6e460a02d1f1
```

You should see `SUCCESS: Specified value was saved.`

**Important: `setx` only affects NEW processes.** Close Studio AND close the terminal window you ran `setx` in. Open a **new** terminal (or just relaunch Studio from Windows Explorer / Start menu) and the env var will be picked up.

Verify it took effect (open a fresh terminal):

```
echo %WEFT_STUDIO_CLIENTID%
```

Should print the GUID. If it prints `%WEFT_STUDIO_CLIENTID%` literally, you're still in the old session — open a new terminal.

Then launch `C:\WeftStudio\WeftStudio.Ui.exe`. The Sign-in button should enable once you paste a valid `powerbi://` URL.

If your tenant blocks the Power BI Desktop ClientId via Conditional Access (you'll get an `AADSTS53003` or similar), ask your IT admin to register a dedicated AAD app for Studio (with delegated `Dataset.Read.All` on Power BI) and use that ClientId instead.

## Read-only snapshot

Workspace-loaded models open as read-only — Save is disabled, an orange banner shows across the top. Use **File → Save As .bim…** to keep a local copy you can edit elsewhere or push back via the `weft deploy` CLI.

## Troubleshooting

- **"Authentication failed for all authenticators"** after a successful browser sign-in: the token reached the server but ADOMD couldn't use it. On Windows this usually means a missing native MSOLAP install — but the self-contained build here ships it. If you still hit this, check that you're signed in to the same tenant that hosts the workspace.
- **Sign-in button stays disabled even after setting the env var:** the env var is read at app launch. Quit Studio (right-click tray icon or Cmd-equivalent) and re-open it.

## Source + docs

- Source: https://github.com/movmarcos/weft (tag `weft-studio-v0.1.1`)
- Docs site (if reachable): https://movmarcos.github.io/weft/docs/studio.html
- License: MIT

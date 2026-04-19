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

Studio is open source — easiest install is **clone + build** from source. You need `git` and the **.NET 10 SDK** installed first. Pick your platform.

> **Network blocks .NET install or release downloads?** Pre-built self-contained Windows x64 binaries live in the repo:
> - **Studio:** [`releases/v0.1.1/`](https://github.com/movmarcos/weft/tree/master/releases/v0.1.1) — clone, unzip, run.
> - **CLI:** [`releases/cli-v1.0.0/`](https://github.com/movmarcos/weft/tree/master/releases/cli-v1.0.0) — clone, single `weft.exe`, no extract needed.
>
> Both bundle the .NET 10 runtime so no install is required.

### Prerequisites — install .NET 10 SDK

**Windows (PowerShell, recommended — uses winget which ships in Windows 10 19+ and 11):**
```powershell
winget install --id Microsoft.DotNet.SDK.10 --source winget
dotnet --version    # should print 10.x.x
```

If `winget` is blocked or missing, download the SDK installer from https://dotnet.microsoft.com/download/dotnet/10.0 (Microsoft URL, usually allowed on corporate networks) and run it. Pick **SDK x64** for Intel/AMD, **SDK Arm64** for ARM Windows.

**macOS (Homebrew):**
```bash
brew install --cask dotnet-sdk
dotnet --version
```

**Linux (Ubuntu/Debian):**
```bash
# add the Microsoft repo first if you don't have it
sudo apt install -y dotnet-sdk-10.0
dotnet --version
```

For other Linux distros, see https://learn.microsoft.com/dotnet/core/install/linux

### Build Studio

#### Windows (x64)

```powershell
git clone https://github.com/movmarcos/weft.git
cd weft
dotnet publish studio/src/WeftStudio.Ui -c Release -r win-x64 --self-contained true -o C:\WeftStudio
C:\WeftStudio\WeftStudio.Ui.exe
```

That produces a self-contained ~130 MB folder under `C:\WeftStudio` and launches the app. Subsequent runs: just double-click `C:\WeftStudio\WeftStudio.Ui.exe` (or pin to Start menu).

#### macOS (Apple Silicon — arm64)

```bash
git clone https://github.com/movmarcos/weft.git
cd weft
dotnet publish studio/src/WeftStudio.Ui -c Release -r osx-arm64 --self-contained true -o ~/Applications/WeftStudio
open ~/Applications/WeftStudio/WeftStudio.Ui
```

For a proper `.app` bundle that shows up in Spotlight, see [the macOS install gist](https://github.com/movmarcos/weft/blob/master/docs/studio.md#macos-app-bundle-optional) below.

> **Connect-to-workspace caveat on macOS:** the `Microsoft.AnalysisServices` TOM library Microsoft ships does not include a macOS-native MSOLAP provider. Open `.bim` works, but **Connect to workspace fails** on macOS with `"Authentication failed for all authenticators"`. Use Windows or Linux x64 for the workspace flow until Microsoft ships macOS support, or v0.2 adds a REST fallback.

#### Linux (x64)

```bash
git clone https://github.com/movmarcos/weft.git
cd weft
dotnet publish studio/src/WeftStudio.Ui -c Release -r linux-x64 --self-contained true -o ~/weftstudio
~/weftstudio/WeftStudio.Ui
```

Linux desktop with X11 / Wayland required (Avalonia handles both).

### Set the AAD ClientId (Connect to workspace only)

Studio v0.1.1 doesn't ship a baked AAD ClientId. To enable the **Sign in** button in the Connect dialog, set `WEFT_STUDIO_CLIENTID` before launch.

The Power BI Desktop public ClientId works in most tenants without IT registering anything new (used by Tabular Editor, ALM Toolkit, etc.):

**Windows (CMD or PowerShell, one-line, persists across reboots):**
```
setx WEFT_STUDIO_CLIENTID 872cd9fa-d31f-45e0-9eab-6e460a02d1f1
```

`setx` only affects new processes — close the terminal you ran it in, then re-launch Studio from Explorer or a fresh terminal.

**macOS / Linux (current shell):**
```bash
export WEFT_STUDIO_CLIENTID=872cd9fa-d31f-45e0-9eab-6e460a02d1f1
```

Add the `export` to your `~/.zshrc` / `~/.bashrc` to persist. On macOS, GUI apps launched from Finder also need `launchctl setenv WEFT_STUDIO_CLIENTID 872cd9fa-d31f-45e0-9eab-6e460a02d1f1` to pick it up.

If your tenant blocks the Power BI Desktop ClientId via Conditional Access, ask your IT admin to register a dedicated AAD app with XMLA Read scope and use its ClientId instead.

### Optional: macOS .app bundle

The `dotnet publish` command above produces the binary but not a Finder-friendly `.app`. To wrap it:

```bash
APP=~/Applications/WeftStudio.app
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS"
cp -R ~/Applications/WeftStudio/. "$APP/Contents/MacOS/"
cat > "$APP/Contents/Info.plist" <<'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>CFBundleExecutable</key><string>WeftStudio.Ui</string>
  <key>CFBundleIdentifier</key><string>com.marcosmagri.weftstudio</string>
  <key>CFBundleName</key><string>Weft Studio</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>0.1.1</string>
  <key>NSHighResolutionCapable</key><true/>
</dict></plist>
EOF
codesign --force --deep --sign - "$APP"
xattr -dr com.apple.quarantine "$APP"
open "$APP"
```

Now it shows up in Spotlight and double-clicks from Finder.

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

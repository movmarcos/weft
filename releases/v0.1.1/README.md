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

For Connect-to-workspace setup, see the [Studio install docs](https://movmarcos.github.io/weft/docs/studio.html#install) — section **Set the AAD ClientId**.

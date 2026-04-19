# Weft CLI v1.0.0 — Windows x64

Self-contained build of the `weft` CLI for users on networks that block downloads from GitHub Releases. Bundles the .NET 10 runtime — **no install required** on the target machine.

## Available builds

| File | Platform | Size | Self-contained? |
|---|---|---|---|
| `weft.exe` | Windows x64 | 92 MB | Yes — no .NET install required |

## Install (Windows x64)

```powershell
git clone https://github.com/movmarcos/weft.git
copy weft\releases\cli-v1.0.0\weft.exe C:\weft\weft.exe

# Add C:\weft to PATH for this session:
$env:Path += ";C:\weft"

# Or permanently via setx (new sessions only):
setx PATH "$env:Path;C:\weft"
```

If Windows SmartScreen says "Windows protected your PC", click **More info → Run anyway**. The build is unsigned (open source, MIT licensed).

## Verify

```powershell
weft --version
weft --help
```

## Common commands

```powershell
# Sanity-check a .bim file (no auth needed)
weft validate --source path\to\model.bim

# Deploy to dev workspace (Interactive auth — opens browser sign-in)
weft deploy --config path\to\weft.yaml --target dev

# Deploy to prod (Service Principal — needs cert / secret in weft.yaml or env vars)
weft deploy --config path\to\weft.yaml --target prod
```

## Auth setup (Connect to Power BI workspaces)

The CLI uses the same AAD app pattern as Studio. For Interactive auth on a dev workspace, set these env vars:

```powershell
setx WEFT_TENANT_ID  <your-tenant-guid>
setx WEFT_CLIENT_ID  <your-aad-app-clientid>

# (open a NEW PowerShell window for setx to take effect)
```

The `weft.yaml` config file references these as `${WEFT_TENANT_ID}` / `${WEFT_CLIENT_ID}`. See `samples/01-simple-bim/weft.yaml` for a starting template.

For Service Principal auth (CI / pipelines), see the [auth docs](https://movmarcos.github.io/weft/docs/authentication.html).

## Source

- Source: https://github.com/movmarcos/weft (CLI lives in `src/Weft.Cli/`)
- Docs: https://movmarcos.github.io/weft/
- License: MIT

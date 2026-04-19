---
title: Getting started with Weft
shortTitle: Get started
eyebrow: Quickstart · Practical
order: 1
color: gold
icon: "↦"
related:
  - authentication
  - parameters
  - hooks
---
# Getting started with Weft

This walkthrough deploys a tiny model to your own Power BI Premium workspace.

## 1. Prerequisites

- A **Power BI Premium capacity** (Premium per user or Premium per capacity / Fabric) with the XMLA endpoint enabled.
- An **AAD app registration** with XMLA Admin rights on the workspace. See [docs/authentication.md](authentication.md) for the cert + SP walkthrough.
- **.NET 10 SDK** locally (`dotnet --version` returns `10.x.x`).

## 2. Install

Either download the binary for your OS from the GitHub Releases page (or `releases/cli-v1.0.0/weft.exe` in the repo for Windows), or build from source:

```os-tabs
@bash
git clone https://github.com/movmarcos/weft.git
cd weft
dotnet publish src/Weft.Cli/Weft.Cli.csproj \
  --configuration Release \
  --runtime osx-arm64 \
  --self-contained false \
  --output ./bin
./bin/weft --help
@powershell
git clone https://github.com/movmarcos/weft.git
cd weft
dotnet publish src\Weft.Cli\Weft.Cli.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output .\bin
.\bin\weft.exe --help
```

## 3. Grab the sample

```os-tabs
@bash
cp -r samples/01-simple-bim /tmp/my-first-weft
cd /tmp/my-first-weft
@powershell
Copy-Item -Recurse samples\01-simple-bim $env:TEMP\my-first-weft
Set-Location $env:TEMP\my-first-weft
```

## 4. Validate

```os-tabs
@bash
weft validate --source ./model.bim
# OK: 'TinyStatic' loaded with 2 table(s).
@powershell
weft validate --source .\model.bim
# OK: 'TinyStatic' loaded with 2 table(s).
```

## 5. Set env vars

```os-tabs
@bash
export WEFT_TENANT_ID='<your tenant guid>'
export WEFT_CLIENT_ID='<your aad app id>'
export WEFT_DEV_WORKSPACE='powerbi://api.powerbi.com/v1.0/myorg/YourDevWorkspace'
export WEFT_DEV_DATABASE='TinyStatic'
@powershell
# Persistent (new sessions only — close and reopen this terminal):
setx WEFT_TENANT_ID     "<your tenant guid>"
setx WEFT_CLIENT_ID     "<your aad app id>"
setx WEFT_DEV_WORKSPACE "powerbi://api.powerbi.com/v1.0/myorg/YourDevWorkspace"
setx WEFT_DEV_DATABASE  "TinyStatic"

# Or just for the current session:
$env:WEFT_TENANT_ID     = "<your tenant guid>"
$env:WEFT_CLIENT_ID     = "<your aad app id>"
$env:WEFT_DEV_WORKSPACE = "powerbi://api.powerbi.com/v1.0/myorg/YourDevWorkspace"
$env:WEFT_DEV_DATABASE  = "TinyStatic"
```

## 6. Deploy

```os-tabs
@bash
weft deploy --config ./weft.yaml --target dev --artifacts ./artifacts
@powershell
weft deploy --config .\weft.yaml --target dev --artifacts .\artifacts
```

First deploy creates the model and refreshes. Second deploy is a no-op.

## 7. Inspect artifacts

```os-tabs
@bash
ls artifacts/
# 20260418-140500-TinyStatic-pre-partitions.json
# 20260418-140500-TinyStatic-plan.tmsl
# 20260418-140500-TinyStatic-post-partitions.json
# 20260418-140500-TinyStatic-receipt.json
@powershell
Get-ChildItem artifacts\
# 20260418-140500-TinyStatic-pre-partitions.json
# 20260418-140500-TinyStatic-plan.tmsl
# 20260418-140500-TinyStatic-post-partitions.json
# 20260418-140500-TinyStatic-receipt.json
```

Every deploy — successful or not — leaves a timestamped trail. Commit these to your release artifacts bucket for audit.

## 8. Next steps

- [Authentication](authentication.md) — setting up SP + cert for CI/CD.
- [Parameters](parameters.md) — per-env DB names and connection strings.
- [Partition preservation](partition-preservation.md) — the core guarantee.
- [Hooks](hooks.md) — notifying Teams/Slack at lifecycle phases.

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

This walkthrough takes a small Power BI semantic model file (`.bim`) and pushes it into your own Power BI Premium workspace using the `weft` command. By the end you'll have:

- The Weft CLI working on your machine.
- A model called **TinyStatic** deployed to your dev workspace.
- A folder full of audit files (called *artifacts*) that prove what changed.

You don't need to be a developer. If you can open a terminal and follow along, you can deploy.

## What Weft does (in one paragraph)

Power BI Desktop saves models as `.bim` files (or as folders, if you use Tabular Editor's *Save to Folder*). To get those models into a live Power BI workspace, normally you'd open Power BI Desktop and click **Publish**, or use a third-party tool like Tabular Editor. **Weft replaces that step with a CLI command** — `weft deploy`. The CLI compares your local `.bim` against what's already on the server and pushes only the difference, **preserving the partitions** that incremental refresh built up over months. No more "publish wipes my data" surprises.

## 1. Prerequisites

You need three things before you start. If you're missing any, see the linked walkthrough.

| What | Why | How to get it |
|---|---|---|
| A **Power BI Premium workspace** with the **XMLA endpoint** enabled | Weft talks to Power BI through XMLA — the same protocol SQL Server Management Studio and Tabular Editor use. Premium per User (PPU), Premium per Capacity, and Fabric all work. The free Power BI tier doesn't have an XMLA endpoint. | Workspace **Settings → Premium** tab → set "XMLA Endpoint" to **Read Write** (your IT admin or capacity admin enables this). |
| An **AAD app registration** with at least *Dataset.ReadWrite.All* on Power BI | This is how `weft` proves to Power BI that it's allowed to act on your behalf. Same idea as the OAuth app behind any other tool that signs in with your work email. | Either reuse one your IT already registered for Power BI tooling, or follow the [Authentication walkthrough](authentication.md) for a fresh registration. You'll need the **Tenant ID** (a GUID identifying your company in Microsoft Entra) and the **Client ID** (a GUID identifying the app itself). |
| **.NET 10 SDK** locally — *only* if you build from source | The CLI is a .NET app. If you grab the pre-built `weft.exe` from the repo (next step), you don't need .NET installed at all. Build-from-source is just an alternative for non-Windows users. | Download from https://dotnet.microsoft.com/download/dotnet/10.0 (Microsoft's site, usually allowed even on locked-down corporate networks). Verify with `dotnet --version` showing `10.x.x`. |

## 2. Install Weft

Two ways. Pick whichever your network allows.

### Option A — Pre-built binary (Windows, no .NET install)

A self-contained `weft.exe` lives in the repo so you can grab it via `git clone` if your network blocks `.exe` downloads from GitHub Releases. It bundles its own .NET runtime, so the target machine needs nothing pre-installed.

```os-tabs
@powershell
git clone https://github.com/movmarcos/weft.git
copy weft\releases\cli-v1.0.0\weft.exe C:\weft\weft.exe
$env:Path += ";C:\weft"
weft --help
```

If Windows SmartScreen says "Windows protected your PC", click **More info → Run anyway**. The build is unsigned (open source, MIT licensed).

### Option B — Build from source (any OS)

Requires the .NET 10 SDK from the prerequisites table.

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

`weft --help` should print a list of commands (`validate`, `deploy`, `refresh`, `restore-history`, etc.). If it does, the CLI is installed.

## 3. Grab the sample model

The repo ships with a few sample models under `samples/`. We'll use the simplest one — `01-simple-bim` — which has two tables (`DimDate`, `FactSales`), one measure, and one relationship. Copy it somewhere outside the repo so your edits don't pollute git.

```os-tabs
@bash
cp -r samples/01-simple-bim /tmp/my-first-weft
cd /tmp/my-first-weft
@powershell
Copy-Item -Recurse samples\01-simple-bim $env:TEMP\my-first-weft
Set-Location $env:TEMP\my-first-weft
```

Inside that folder you'll see:

- `model.bim` — the model definition in JSON form.
- `weft.yaml` — the Weft config (which workspace to deploy to, which auth mode, which env vars to read).

## 4. Validate the model (no auth needed)

Before connecting to anything live, sanity-check that the `.bim` file actually parses. This is a pure local file check — no network, no sign-in.

```os-tabs
@bash
weft validate --source ./model.bim
# OK: 'TinyStatic' loaded with 2 table(s).
@powershell
weft validate --source .\model.bim
# OK: 'TinyStatic' loaded with 2 table(s).
```

What this proves: the JSON is syntactically valid, the TOM (Tabular Object Model) library can load it, and the model has the structure Weft expects. If you ever get a parse error from `weft deploy`, run `validate` first to isolate "is the file broken" from "is the deploy broken".

## 5. Set environment variables

Weft reads four values from your shell environment. The `weft.yaml` in this sample references them as `${WEFT_TENANT_ID}`, `${WEFT_CLIENT_ID}`, etc.

| Variable | What it is | Where to find it |
|---|---|---|
| `WEFT_TENANT_ID` | The GUID of your Microsoft Entra (AAD) tenant — your company's directory. | Microsoft Entra admin centre → **Overview** page → Tenant ID. Or your IT can give it to you. |
| `WEFT_CLIENT_ID` | The GUID of the AAD app registration from prerequisites. | Microsoft Entra admin centre → **App registrations** → your app → **Application (client) ID**. |
| `WEFT_DEV_WORKSPACE` | The XMLA URL of the workspace you want to deploy to. | Power BI Service → your workspace → **Settings** → **Premium** tab → "Workspace Connection". Format: `powerbi://api.powerbi.com/v1.0/myorg/<workspace-name>`. |
| `WEFT_DEV_DATABASE` | The dataset name as it should appear in the workspace. | You choose this. For our sample, `TinyStatic`. If a dataset by that name exists in the workspace, Weft will *update* it; otherwise it creates a new one. |

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

> **Heads-up on `setx` (PowerShell):** `setx` writes to the user-level Windows registry, so the value persists across reboots — but it does NOT update the current PowerShell window. After running `setx`, **close that PowerShell window and open a fresh one**, otherwise `weft` won't see the new values. Use the `$env:VAR = ...` form (in the same code block) if you only need the value for the current session.

## 6. Deploy to your workspace

This is the moment the model goes from your laptop into Power BI. Run this from inside the sample folder:

```os-tabs
@bash
weft deploy --config ./weft.yaml --target dev --artifacts ./artifacts
@powershell
weft deploy --config .\weft.yaml --target dev --artifacts .\artifacts
```

### What the flags mean

| Flag | Meaning |
|---|---|
| `--config ./weft.yaml` | Tells Weft which configuration file to read. The YAML defines auth mode, target workspaces, parameters, hooks. The sample's YAML is short — open it and read it. |
| `--target dev` | Picks the `dev` profile from the YAML. The same YAML can define `prod`, `uat`, etc. — `--target` selects one. |
| `--artifacts ./artifacts` | Tells Weft to write its audit trail (more on this in step 7) into a folder called `./artifacts/`. If you skip this flag, Weft uses a default folder under your home directory. |

### What actually happens during a deploy

In order:

1. **Auth.** Weft signs in to Microsoft Entra using the credentials from `weft.yaml` + your env vars. For the sample's `dev` profile this is **Interactive** auth — your default browser opens, you sign in with your work email, and consent to the AAD app. After that, MSAL caches the token so subsequent runs skip the browser step (silent re-auth).
2. **Read source.** Weft loads `model.bim` from disk, applies parameters (none in this sample), and produces an in-memory model.
3. **Read target.** Weft connects to your workspace's XMLA endpoint and fetches the current state of the `TinyStatic` dataset (or notes that it doesn't exist yet).
4. **Diff.** It compares source vs target — what tables/columns/measures/partitions need to be added, dropped, or altered.
5. **Safety gates.** If your YAML doesn't allow drops (`allowDrops: false`) and the diff would drop a table, Weft refuses with exit code `5`. Same for history loss on incremental-refresh tables.
6. **Plan TMSL.** It generates a TMSL (Tabular Model Scripting Language) script — the JSON command sequence the Power BI engine will execute.
7. **Execute TMSL.** The plan is sent to the workspace and run. After this step, the workspace has the new model schema.
8. **Refresh.** Weft requests a refresh on every changed table. The Power BI engine pulls data from the source defined in each table's M expression and populates partitions.
9. **Write artifacts.** Every step's input, output, and outcome is written to `./artifacts/` as JSON files.

If you watch your terminal, you'll see each step log a one-liner. If anything fails, Weft stops and exits with a non-zero code (see [exit codes](/exit-codes/)).

### What success looks like

- The terminal prints `Deploy complete in <N>ms` and exits with code `0`.
- In Power BI Service, your workspace now contains a dataset called `TinyStatic` (refresh icon visible, last-refresh time = a few seconds ago).
- The `./artifacts/` folder has four new files (next step).

### What "no-op" means

Run `weft deploy ...` again with no changes to `model.bim` — Weft will detect that source matches target, skip the TMSL execute step, and still log a successful deploy. **A no-op deploy is not an error** — it's confirmation that source and target are already in sync. You'll see something like `No changes detected — nothing to deploy. Refresh skipped.`

## 7. Inspect the artifacts

Weft leaves a timestamped trail of every deploy under the `--artifacts` folder. This is the audit story — you can answer "what did the deploy do?" months later by reading these files.

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

| File | What's in it |
|---|---|
| `*-pre-partitions.json` | Snapshot of every table's partitions on the **target** before the deploy started. If the deploy goes wrong, this is your "what was there before" record. |
| `*-plan.tmsl` | The TMSL script Weft generated and executed. Open it to see exactly what Power BI was told to do — useful for code review or replaying manually. |
| `*-post-partitions.json` | Snapshot of partitions **after** the deploy. Diff this against `pre-partitions.json` to see what changed. |
| `*-receipt.json` | Summary record: profile name, exit code, duration, hook outputs, refresh status per table. The deploy's "headline". |

In a real CI pipeline, these go to your release-artifacts bucket (S3 / Azure Blob / Octopus) so an auditor can answer "what did we deploy on March 4 and did it succeed?" by reading one folder.

## 8. Next steps

You've done the basic flow. From here:

- **[Authentication](authentication.md)** — setting up Service Principal + certificate for CI/CD pipelines. Interactive auth from step 5 is fine for laptops; CI needs unattended auth.
- **[Parameters](parameters.md)** — same `.bim` ships to dev and prod with different `DatabaseName` / `ServerName` values, no copy-paste model files.
- **[Partition preservation](partition-preservation.md)** — the core guarantee. Read this before deploying any model with incremental refresh.
- **[Hooks](hooks.md)** — notifying Teams/Slack at lifecycle phases, scaling capacity up/down, blocking deploys on approvals.
- **[Weft Studio](studio.md)** — the desktop GUI for browsing and editing models, sibling to the CLI.

## Troubleshooting

If step 6 fails:

| Symptom | Likely cause | Where to look |
|---|---|---|
| Browser opens but sign-in fails with `AADSTS...` | Wrong tenant / blocked app / no consent | [Authentication](authentication.md) and [Troubleshooting](troubleshooting.md) |
| `Authentication failed for all authenticators` | The token reached XMLA but was rejected — usually a scope/audience problem with the AAD app | [Authentication](authentication.md) |
| `Workspace not found` | `WEFT_DEV_WORKSPACE` URL wrong, or the AAD app doesn't have access to that workspace | Power BI Service → workspace → **Access** — check the AAD app or your user is listed |
| Deploy refused with exit `5` (`DiffValidationError`) | The diff would drop something the YAML didn't allow | Read the error — it names the table/measure being dropped. Either intentional (set `allowDrops: true`) or your source is missing something |
| Deploy refused with exit `6` (`HistoryLossGate`) | Shrinking an incremental refresh window would evict partitions | [Incremental refresh](incremental-refresh.md) — explains `allowHistoryLoss` |

Full exit-code list: [exit codes reference](/exit-codes/).

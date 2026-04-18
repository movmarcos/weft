# Weft

> Diff-based deploys for Power BI semantic models — without losing your partitions.

`weft` is a cross-platform .NET CLI that deploys Power BI / Fabric semantic models over the XMLA endpoint. Unlike a full `createOrReplace`, it compares your `.bim` source against the live target and emits a surgical TMSL script that:

- Creates new tables from source.
- Alters changed tables **without touching their partitions** — historical partitions created by refresh policies stay intact.
- Preserves every partition's `RefreshBookmark` annotation.
- Blocks deploys that would silently shrink an incremental-refresh rolling window (history-loss gate).
- Runs hooks at each lifecycle phase (pre-deploy, post-deploy, on-failure, etc.).
- Resolves model parameters per environment from a YAML profile.

## Quickstart

```bash
# Install .NET 10 SDK, then:
git clone https://github.com/marcosmagri/weft.git
cd weft
dotnet build

# Validate a .bim file
./src/Weft.Cli/bin/Debug/net10.0/weft validate --source samples/01-simple-bim/model.bim

# Deploy (needs a Premium workspace + AAD app with XMLA Admin permission)
./src/Weft.Cli/bin/Debug/net10.0/weft deploy \
  --config samples/01-simple-bim/weft.yaml \
  --target dev
```

See [docs/getting-started.md](docs/getting-started.md) for a walkthrough.

## Commands

| Command | What it does |
|---|---|
| `weft validate --source <path>` | Parse and validate a `.bim` or TE folder model. |
| `weft plan --source <.bim> --target-snapshot <.bim>` | Offline diff against a captured target snapshot. |
| `weft deploy --config weft.yaml --target <profile>` | Full pipeline: auth → diff → execute → refresh. |
| `weft refresh --tables a,b,c` | Refresh selected tables. |
| `weft restore-history --table <name>` | Re-materialize historical partitions per policy. |
| `weft inspect partitions --target-snapshot <.bim>` | List partitions and bookmarks from a snapshot. |

## Authentication

Five modes, all via MSAL:

- `ServicePrincipalCertStore` — Windows cert store (ops / Octopus).
- `ServicePrincipalCertFile` — `.pfx` file + password.
- `ServicePrincipalSecret` — client secret (last resort).
- `Interactive` — browser popup (dev).
- `DeviceCode` — headless dev boxes.

See [docs/authentication.md](docs/authentication.md).

## Integrations

- **GitHub Actions:** `.github/workflows/ci.yml` for PR CI, `release-artifacts.yml` for binaries + NuGet on tag.
- **TeamCity:** `build/teamcity/settings.kts` (Kotlin DSL).
- **Octopus Deploy:** `build/octopus/step-templates/weft-deploy.json` — import once, use in any project.

## Documentation

- [Getting Started](docs/getting-started.md)
- [Authentication](docs/authentication.md)
- [Parameters](docs/parameters.md)
- [Partition Preservation](docs/partition-preservation.md)
- [Incremental Refresh](docs/incremental-refresh.md)
- [Restore History](docs/restore-history.md)
- [Hooks](docs/hooks.md)
- [Troubleshooting](docs/troubleshooting.md)

The same docs are available as a browseable site at <https://marcosmagri.github.io/weft/> (after first push to GitHub Pages).

## License

MIT. See [LICENSE](LICENSE).

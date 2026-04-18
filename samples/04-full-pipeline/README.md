# Sample 04 — Full pipeline with hooks

Shows every Weft lifecycle piece in one place: SP cert auth, hooks on preDeploy / postDeploy / onFailure, and safe defaults for prod (`allowDrops: false`, `allowHistoryLoss: false`).

## Hook protocol

On each phase, Weft spawns the configured command as a child process. The child receives:

- **stdin**: `HookContext` JSON (`{ profileName, workspaceUrl, databaseName, phase, changeSet: { added, dropped, altered, unchanged } }`).
- **env**: `WEFT_HOOK_PHASE`, `WEFT_HOOK_PROFILE`, `WEFT_HOOK_DATABASE` (convenience).
- Sanitized env: known secret-bearing variables (`WEFT_CLIENT_SECRET`, `WEFT_CERT_PASSWORD`, `WEFT_CERT_THUMBPRINT`, any `WEFT_PARAM_*` containing PASSWORD/SECRET/KEY/TOKEN) are **removed** before spawn.

## Writing hooks

Unix: `./hooks/notify.sh` (chmod +x). Windows: `./hooks/notify.ps1`. Cross-platform: use `pwsh` and target either. See [docs/hooks.md](../../docs/hooks.md).

## Usage

```bash
export WEFT_TENANT_ID=...
export WEFT_SP_CLIENT_ID=...
export WEFT_CERT_THUMBPRINT=...
export WEFT_PROD_WORKSPACE='powerbi://...'
export WEFT_PROD_DATABASE='TinyStatic'

chmod +x hooks/notify.sh
weft deploy --config ./weft.yaml --target prod
```

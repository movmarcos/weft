# Hooks

Weft fires hooks at six pipeline phases. Each hook is an external command you configure in `weft.yaml`.

## Phases

| Phase | When it fires | Typical use |
|---|---|---|
| `prePlan` | Before diff is computed | Announce "starting deploy" |
| `preDeploy` | After plan, before TMSL execute | Require approval, lock a change-window |
| `postDeploy` | After TMSL succeeds, before refresh | Tag a git release |
| `preRefresh` | Before refresh TMSL | Scale up capacity |
| `postRefresh` | After refresh succeeds | Scale down, notify success |
| `onFailure` | On any deploy-phase failure | Page oncall, open an incident |

## Config

```yaml
hooks:
  preDeploy: ./hooks/pre-deploy.sh
  postDeploy: ./hooks/tag-git-release.sh
  preRefresh: ./hooks/scale-up.ps1
  postRefresh: ./hooks/scale-down.ps1
  onFailure: ./hooks/open-incident.sh
```

Paths are relative to where you run `weft` (or absolute).

## Protocol

Weft spawns the command as a child process. The child receives:

- **stdin**: HookContext JSON:
  ```json
  {
    "profileName": "prod",
    "workspaceUrl": "powerbi://api.powerbi.com/v1.0/myorg/Weft-Prod",
    "databaseName": "SalesModel",
    "phase": "PreDeploy",
    "changeSet": {
      "added": ["NewTable"],
      "dropped": [],
      "altered": ["FactSales"],
      "unchanged": ["DimDate"]
    }
  }
  ```
- **env** (convenience):
  - `WEFT_HOOK_PHASE` — matches `changeSet.phase`
  - `WEFT_HOOK_PROFILE` — profile name
  - `WEFT_HOOK_DATABASE` — database name

## Environment sanitization

Before spawning, Weft removes these variables from the child environment:

- `WEFT_CLIENT_SECRET`
- `WEFT_CERT_PASSWORD`
- `WEFT_CERT_THUMBPRINT`
- Any `WEFT_PARAM_*` whose name contains `PASSWORD`, `SECRET`, `KEY`, or `TOKEN` (case-insensitive).

Secrets you want accessible in hooks should come from your own env config (Octopus sensitive variables, GitHub Actions secrets, etc.) under a non-reserved name.

## Failure behaviour

A hook that exits non-zero logs a warning and **does not abort** the main deploy (except `onFailure` hooks, which fire *after* a failure anyway). This is deliberate: a failing `postDeploy` notification shouldn't roll back a successful TMSL alter.

If you want "block on hook failure" semantics, script the hook to `exit 0` on warnings and use `onFailure` for real aborts — or write a custom pre-deploy hook that fails the deploy via stdout+nonzero: Weft will log but continue, so the main deploy still proceeds. (Blocking hooks are a feature tracked for a future release.)

## Command parsing — WHITESPACE SPLIT, NOT SHELL

The `command:` string is split on whitespace. Quoted arguments **are not supported**:

```yaml
hooks:
  preDeploy: ./hooks/notify.sh "arg with spaces"   # BREAKS: 3 args, not 1
```

Wrap complex args in a shell script:

```yaml
hooks:
  preDeploy: ./hooks/notify.sh
```

```bash
#!/bin/sh
ARG="arg with spaces"
./actual-command.sh "$ARG"
```

## Example hooks

See `samples/04-full-pipeline/hooks/` for a cross-platform pair:

- `notify.sh` — Unix shell reading JSON from stdin, printing a summary.
- `notify.ps1` — PowerShell equivalent.

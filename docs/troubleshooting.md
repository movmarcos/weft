# Troubleshooting

## Exit codes

| Code | Meaning | Next step |
|---|---|---|
| 0 | Success | — |
| 2 | Config error (malformed `weft.yaml`, missing env var) | Check `weft.yaml` syntax; `echo $WEFT_*` |
| 3 | Auth error | See [authentication.md](authentication.md) |
| 4 | Source load error | `weft validate --source ...` to isolate |
| 5 | Target read error (XMLA connection) | Check workspace URL, SP permissions |
| 6 | Diff validation error (drop without `--allow-drops`, history-loss without `allowHistoryLoss`) | Read the error, decide if opting in is safe |
| 7 | TMSL execution error | Check the returned XMLA message; check `artifacts/*-plan.tmsl` |
| 8 | Refresh error | Model deployed, data is stale — re-run `weft refresh` |
| 9 | Partition integrity violation | STOP. Do not retry. Investigate with `artifacts/*-pre-partitions.json` vs `*-post-partitions.json` |
| 10 | Parameter error (required parameter missing, type mismatch) | Check `weft.yaml` parameter declarations and profile values |

## Common issues

### `Source not found: /path/to/model.bim`

`weft deploy` can't find your source. Check:

- Path is relative to the **CWD where you run `weft`**, not to `weft.yaml`.
- If using `--config`, omit `--source` and let the config's `source.path` apply.

### `Environment variable 'WEFT_TENANT_ID' referenced in config is not set.`

`${VAR}` expansion couldn't find the variable. Either `export` it, or set it in Octopus / TeamCity / GitHub Actions secrets.

### `Partition integrity violation: table 'FactSales' missing post-deploy.`

Something outside Weft deleted the table between pre-manifest and post-manifest. Causes:

- Another process ran `createOrReplace` during your deploy.
- The workspace was migrated / the dataset was swapped.

Action: STOP. Investigate before re-running. Weft's receipt (`artifacts/*-receipt.json`) + manifests are the forensic record.

### `Partition integrity violation on 'FactSales': missing post-deploy: Year2021, Year2022`

The post-deploy manifest is missing partitions that existed pre-deploy on a **preserved** table. This is the §5.4 invariant violation. Weft refused to emit the bad TMSL, so you only see this if:

- Another TMSL ran out-of-band and clobbered the table during your deploy.
- There's a bug in `TmslBuilder` — please file an issue with the `artifacts/` contents.

### `History-loss violation on FactSales: would remove Year2021, Year2022, Year2023`

You're shrinking an incremental-refresh `rollingWindowPeriods`. See [incremental-refresh.md](incremental-refresh.md) for the `allowHistoryLoss: true` opt-in and `weft restore-history` recovery.

### `Refusing to drop tables without allowDrops: LegacyDim`

Source removed a table that exists on target. Either:

- Genuinely want to drop it: `weft deploy ... --allow-drops` (and set `allowDrops: true` in the profile).
- Or restore the table in your source (uncommit the deletion).

### `Hook 'PreDeploy' exited 1 (non-fatal).`

Your `preDeploy` hook returned non-zero. Weft logged the failure and continued. Check the hook's stderr in the deploy log. If you want hook failures to stop the deploy, exit with code 0 on warnings and emit diagnostics to stderr.

### MSAL `InteractionRequired` on service-principal mode

The SP's client secret expired or the cert is wrong. In the Azure portal, re-issue the cert or the secret, and update your env vars.

## Getting help

File an issue using `.github/ISSUE_TEMPLATE/bug_report.md`. Include:

- Exit code.
- `weft plan` output (if the issue is during deploy).
- The `artifacts/*-plan.tmsl` file (redact any secrets manually).
- .NET SDK version + OS.

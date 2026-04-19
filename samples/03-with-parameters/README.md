# Sample 03 — Parameterized model

Shows per-environment parameter injection. The same `.bim` ships to dev and prod with different `DatabaseName` / `ServerName` values.

## The model

Two M parameters, `DatabaseName` and `ServerName`, declared with `IsParameterQuery=true`. Their defaults are `"EDW"` and `"localhost"` — production-safe fallbacks that a deploy without config would use.

## The config

The `parameters:` block declares required parameters at the top level. Each profile's `parameters:` map provides per-env values. At deploy time, Weft:

1. Auto-discovers every `IsParameterQuery` expression in the source model.
2. Resolves each from (priority order): CLI `--param KEY=VALUE` → `--params-file` → env `WEFT_PARAM_<name>` → profile YAML → declaration default.
3. Fails the deploy if a `required: true` parameter has no value anywhere.
4. Rewrites the M expression literal in-memory, preserving any `meta [...]` suffix.

## Usage

```os-tabs
@bash
weft deploy --config ./weft.yaml --target prod
# → rewrites DatabaseName → "EDW_PROD", ServerName → "prod-sql.corp.local"
#   before diffing against target
@powershell
weft deploy --config .\weft.yaml --target prod
# → rewrites DatabaseName → "EDW_PROD", ServerName → "prod-sql.corp.local"
#   before diffing against target
```

Ad-hoc override (e.g., a hotfix deploy):

```os-tabs
@bash
export WEFT_PARAM_DatabaseName="EDW_PROD_HOTFIX"
weft deploy --config ./weft.yaml --target prod
# → DatabaseName="EDW_PROD_HOTFIX" wins over the profile YAML
@powershell
$env:WEFT_PARAM_DatabaseName = "EDW_PROD_HOTFIX"
weft deploy --config .\weft.yaml --target prod
# → DatabaseName="EDW_PROD_HOTFIX" wins over the profile YAML
```

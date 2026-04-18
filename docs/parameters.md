# Parameters

Weft auto-discovers every M parameter in your source model and lets you override its value per environment.

## What counts as a parameter

Any `NamedExpression` in `model.expressions` with:
- `kind: "m"`
- An `annotations` entry named `IsParameterQuery` with value `"true"`

This is the shape Power BI Desktop creates when you define a parameter via **Manage parameters**.

## Resolution priority (highest wins)

1. CLI: `--param DatabaseName=EDW_PROD_HOTFIX` (repeatable).
2. Params file: `--params-file ./my-params.json` (coming in a future release).
3. Env var: `WEFT_PARAM_DatabaseName`.
4. Profile YAML: `profiles.<env>.parameters.DatabaseName`.
5. Declaration default: `parameters[].default` in the top-level `parameters:` list (or the M expression's literal).

If a `required: true` declaration has no value anywhere, the deploy fails with exit code `10` (`ParameterError`).

## Declaring parameters

```yaml
parameters:
  - name: DatabaseName
    description: Warehouse database name
    type: string
    required: true
  - name: ServerName
    type: string
    required: true
  - name: EnableDebugMeasures
    type: bool
    required: false
```

Types: `string`, `bool`, `int`. Weft coerces YAML scalars → M literals (`"EDW"` → `"EDW"`, `true` → `true`, `42` → `42`, with proper M escaping for strings containing quotes).

## Applying per-env values

```yaml
profiles:
  dev:
    parameters:
      DatabaseName: EDW_DEV
      ServerName: dev-sql.corp.local
      EnableDebugMeasures: true

  prod:
    parameters:
      DatabaseName: EDW_PROD
      ServerName: prod-sql.corp.local
      # EnableDebugMeasures unset → falls through to declaration default / model literal
```

## How it works under the hood

At deploy time (phase 2a):
1. `ParameterResolver` builds a `(name → value, source)` map for every declared parameter.
2. `MParameterDiscoverer` finds every `IsParameterQuery` expression in the source model.
3. For each resolution, `ParameterValueCoercer` emits the M literal (`"EDW_PROD"` for string, etc.).
4. The M expression is rewritten: `"EDW" meta [...]` → `"EDW_PROD" meta [...]`. The `meta [...]` suffix is preserved.

The diff then sees the resolved model, so parameters travel with the TMSL to the target.

## Hotfix overrides without editing YAML

```bash
export WEFT_PARAM_DatabaseName="EDW_PROD_HOTFIX"
weft deploy --config weft.yaml --target prod
```

Or one-off:

```bash
weft deploy --config weft.yaml --target prod --param DatabaseName=EDW_PROD_HOTFIX
```

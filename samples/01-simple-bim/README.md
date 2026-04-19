# Sample 01 — Simple `.bim`

The minimal Weft setup: a single `.bim` file, no parameters, no hooks.

## Model

`TinyStatic` — two tables (`DimDate`, `FactSales`), one measure, one relationship.

## Environment

Set these env vars, then run:

```os-tabs
@bash
export WEFT_TENANT_ID='<your tenant id>'
export WEFT_CLIENT_ID='<your app id>'
export WEFT_DEV_WORKSPACE='powerbi://api.powerbi.com/v1.0/myorg/YourDevWorkspace'
export WEFT_DEV_DATABASE='TinyStatic'

weft validate --source ./model.bim
weft deploy --config ./weft.yaml --target dev
@powershell
$env:WEFT_TENANT_ID     = "<your tenant id>"
$env:WEFT_CLIENT_ID     = "<your app id>"
$env:WEFT_DEV_WORKSPACE = "powerbi://api.powerbi.com/v1.0/myorg/YourDevWorkspace"
$env:WEFT_DEV_DATABASE  = "TinyStatic"

weft validate --source .\model.bim
weft deploy --config .\weft.yaml --target dev
```

The first `deploy` creates the two tables and runs a refresh. The second is a no-op (nothing changed).

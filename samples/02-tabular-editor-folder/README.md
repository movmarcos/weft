# Sample 02 — Tabular Editor "Save to Folder"

This demonstrates `format: folder` sources — the layout Tabular Editor produces when you use **Save to Folder** instead of a single `.bim`.

## Layout

```
Model/
  database.json
  tables/
    DimDate.json
    FactSales.json
```

Weft stitches the per-table JSON files back into the `database.json` in memory and deserializes through the standard TOM serializer.

## Usage

```bash
export WEFT_TENANT_ID='...'
export WEFT_CLIENT_ID='...'
export WEFT_DEV_WORKSPACE='powerbi://api.powerbi.com/v1.0/myorg/YourDevWorkspace'
export WEFT_DEV_DATABASE='TinyFolder'

weft validate --source ./Model
weft deploy --config ./weft.yaml --target dev
```

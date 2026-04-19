# Sample 05 — With Roles (RLS demo)

A small `.bim` with the same shape as Sample 01 (Sales fact + Date dimension), plus **four security roles** to exercise Studio's read-only Inspector against `Role` objects.

## Model

`RolesDemo` — two tables, three measures, one relationship, **four roles**:

| Role | `modelPermission` | Row filter |
|---|---|---|
| `Read All` | `read` | none — full access |
| `East Region Only` | `read` | `Sales[Region] = "East"` |
| `Refresh Only` | `refresh` | service-account pattern |
| `No Access` | `none` | explicit deny |

## What it shows in Studio

Open `model.bim` in Weft Studio (File → Open .bim, or `dotnet run --project studio/src/WeftStudio.Ui` then File → Open .bim, point at `samples/05-with-roles/model.bim`).

Expand the tree:

```
Tables
├── DimDate
│   ├── Date           ← click to inspect column
│   └── Year
└── Sales
    ├── Date
    ├── Region
    ├── Amount
    ├── Total Sales    ← click to inspect measure + see DAX in middle pane
    ├── East Sales
    └── Sales YoY %
Measures
├── Sales[Total Sales]
├── Sales[East Sales]
└── Sales[Sales YoY %]
Relationships
└── rel_sales_dimdate
Roles
├── Read All           ← modelPermission: read
├── East Region Only   ← row filter on Sales
├── Refresh Only       ← modelPermission: refresh
└── No Access          ← modelPermission: none
```

Click any role to inspect its `Name`, `ModelPermission`, `Description`, etc. The row filter expression is a property of each `TablePermission` (a child object) — the v0.1.x scalar inspector won't drill into that yet; v0.2 will add per-type panels.

## CLI deploy

To deploy to a dev workspace (configure env vars per Sample 01):

```bash
weft validate --source ./model.bim
weft deploy --config ./weft.yaml --target dev
```

After deploy, assign AAD groups / users to the appropriate roles via Power BI Service → workspace settings → semantic model → Security.

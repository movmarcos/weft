---
title: Restore history
shortTitle: Restore
eyebrow: Restore · Safety-critical
order: 6
color: pink
icon: "⤴"
related:
  - partition-preservation
  - incremental-refresh
  - troubleshooting
---
# Restore history

If historical partitions are missing — because of a manual `allowHistoryLoss` shrink, a botched deploy, or a warehouse re-seed — `weft restore-history` re-materializes them.

## When to use it

- After `allowHistoryLoss: true` dropped partitions you now need back.
- After a manual TMSL execution outside Weft lost partitions.
- When bringing a test environment back to parity with prod.

## Prerequisites

The target table must still have a `RefreshPolicy` (so the engine knows how to materialize). The warehouse source must still contain the historical data you want to restore.

## Usage

```os-tabs
@bash
weft restore-history \
  --config weft.yaml \
  --target prod \
  --table FactSales \
  --from 2020-01-01 \
  --to 2023-12-31 \
  --effective-date 2023-12-31
@powershell
weft restore-history `
  --config weft.yaml `
  --target prod `
  --table FactSales `
  --from 2020-01-01 `
  --to 2023-12-31 `
  --effective-date 2023-12-31
```

Under the hood this issues a TMSL `refresh` with `type: "full"`, `applyRefreshPolicy: true`, and `effectiveDate` set. The engine walks the policy's source expression with `RangeStart` / `RangeEnd` bound to each period in the requested range.

## Options

- `--table` (required) — the table to restore.
- `--from` / `--to` (optional) — ISO dates. Used as the `effectiveDate` window if provided; otherwise the policy's natural rolling window applies.
- `--effective-date` — explicit override; defaults to `--to` or today.

## Caveats

- **Only recovers what the source still has.** If the warehouse archived `2020` data, restoring it will re-materialize an empty partition. Verify source retention before running.
- **Can be slow.** Restoring 5 years of monthly partitions on a 10B-row fact table can run for hours. Schedule accordingly.
- **Locks the table for the duration.** Concurrent reports querying `FactSales` during restore will see the old data until the refresh completes.

## Example scenario

"We shrunk the rolling window from 5y to 3y last quarter. Finance now needs Q4-2021 back for audit."

```os-tabs
@bash
weft restore-history --config weft.yaml --target prod \
  --table FactSales --from 2021-10-01 --to 2021-12-31
@powershell
weft restore-history --config weft.yaml --target prod `
  --table FactSales --from 2021-10-01 --to 2021-12-31
```

If the warehouse has the data, Q4-2021 partitions reappear. If not, the partitions reappear empty and you need a data team to replay.

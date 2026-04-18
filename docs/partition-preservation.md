---
title: Partition preservation — the core guarantee
eyebrow: Partitions · Safety-critical
order: 4
color: pink
icon: "▦"
related:
  - incremental-refresh
  - restore-history
  - troubleshooting
---
# Partition preservation — the core guarantee

This is Weft's load-bearing promise: **no partition created by a prior refresh is dropped by a deploy.**

## The problem

When Power BI Desktop publishes a model, it does a `createOrReplace` on the whole database. That wipes every partition the refresh cycle has created since the last publish — including the historical partitions that an incremental-refresh policy materialized months or years ago. Since the default refresh only touches the last N days, those historical partitions don't come back automatically.

For tenants running dynamic partitioning (a custom script creates a new partition per month and refreshes it once), a `createOrReplace` is worse: it drops every dynamic partition, forcing a full rebuild.

## The Weft approach

Weft does a three-way diff before emitting TMSL:

1. **Source** — your committed `.bim` or TE folder.
2. **Target** — the live model read via XMLA.
3. **ChangeSet** — what needs to change.

Every table falls into one of three classifications (see `Weft.Core.Diffing.TableClassification`):

- **Static** — no refresh policy, partitions in source match target. TMSL: no change.
- **DynamicallyPartitioned** — target has partitions not present in source. Weft preserves target's partition collection; schema changes apply to columns/measures only.
- **IncrementalRefreshPolicy** — the table has a `RefreshPolicy` object. Weft preserves partitions and delegates rolling-window management to Power BI's refresh engine.

For classes 2 and 3, the emitted TMSL is an `alter` operation on the table's schema that **explicitly re-attaches every existing partition from target**, including each partition's `RefreshBookmark` annotation. The `PartitionIntegrityValidator` sanity-checks the generated TMSL before it's sent.

## What's in the TMSL

For an altered `FactSales` table that had 48 historical partitions:

```json
{
  "createOrReplace": {
    "object": { "database": "SalesModel", "table": "FactSales" },
    "table": {
      "name": "FactSales",
      "columns": [...],
      "measures": [...],
      "partitions": [
        { "name": "Year2020", "source": {...}, "annotations": [{"name":"RefreshBookmark","value":"2021-12-31T23:59:59Z"}] },
        { "name": "Year2021", ... },
        ...
        { "name": "Month2026-04", ... }
      ]
    }
  }
}
```

All 48 partitions travel through the alter. Their bookmarks come along too — so "detect data changes" still knows what's fresh.

## Integrity checks

Two gates run on every deploy:

1. **Pre-emit** (`PartitionIntegrityValidator`): scans the generated TMSL for any partition delete targeting a preserved table. Throws `PartitionIntegrityException` before the TMSL leaves the CLI.
2. **Post-deploy** (integrity gate in `DeployCommand`): re-reads the target, compares the partition manifest to the pre-deploy snapshot. Any preserved table that lost partitions → exit code `9` (`PartitionIntegrityError`).

## What you need to do

Nothing. This is the default behaviour for every deploy.

The only case requiring your input is a deliberate rolling-window shrink — see [incremental-refresh.md](incremental-refresh.md).

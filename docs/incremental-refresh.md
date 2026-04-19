---
title: Incremental refresh
shortTitle: Incremental
eyebrow: Refresh · Safety-critical
order: 5
color: pink
icon: "↻"
related:
  - partition-preservation
  - restore-history
  - troubleshooting
---
# Incremental refresh

Weft understands Power BI's `RefreshPolicy` object and treats incremental-refresh tables specially.

## What Weft looks for

A `Table` with a non-null `RefreshPolicy` (a `BasicRefreshPolicy` in TOM):

```json
{
  "name": "FactSales",
  "refreshPolicy": {
    "policyType": "basic",
    "rollingWindowGranularity": "year",
    "rollingWindowPeriods": 5,
    "incrementalGranularity": "day",
    "incrementalPeriods": 10,
    "sourceExpression": [ "let Source = ... in Source" ]
  }
}
```

Weft classifies this table as `IncrementalRefreshPolicy`. Partitions are preserved unconditionally (see [partition-preservation.md](partition-preservation.md)); the refresh engine's `ApplyRefreshPolicy` semantics handle the rolling window.

## The history-loss gate

If you shrink `rollingWindowPeriods` (e.g., from 5 years to 3 years), the refresh engine will **drop the oldest partitions** next time `ApplyRefreshPolicy=true`. Weft refuses this by default.

### What happens at deploy

Phase 5a runs `HistoryLossGate`:

```
FactSales: rollingWindowPeriods 5 → 3 would evict partitions:
  Year2021, Year2022, Year2023
Refusing deploy without profile.allowHistoryLoss=true.
Exit code: 6 (DiffValidationError)
```

### When you actually want the shrink

```yaml
profiles:
  prod:
    allowHistoryLoss: true     # explicit opt-in
```

Now the deploy proceeds; the refresh engine prunes old years; `restore-history` can bring them back if the warehouse still has the data.

## Per-table refresh-type matrix

After every deploy, Weft refreshes affected tables via `RefreshRunner`. The refresh type is chosen per table (see `Weft.Xmla.RefreshTypeSelector`):

| Classification | New (added) | Altered w/ policy change | Altered w/ schema only |
|---|---|---|---|
| `IncrementalRefreshPolicy` | `Policy` + `ApplyRefreshPolicy=true` | `Policy` + `ApplyRefreshPolicy=true` | `Policy` (no apply) |
| `DynamicallyPartitioned` | `Full` on all partitions | `Full` on all partitions | `Full` on all partitions |
| `Static` | `Full` | `Full` | `Full` |

The `Policy` refresh lets the engine roll the window forward (and/or apply a new policy) without Weft orchestrating per-partition refreshes.

## Bookmark modes

`RefreshBookmark` is the "detect data changes" watermark. Weft preserves it across deploys by default. You can override per profile:

```yaml
defaults:
  refresh:
    incrementalPolicy:
      bookmarkMode: preserve    # preserve | clearAll | clearForPolicyChange
```

- `preserve` — keep bookmarks (default).
- `clearAll` — emit an annotation-delete TMSL before refresh so every partition re-checks its source.
- `clearForPolicyChange` — clear only on tables whose policy changed this deploy.

CLI shortcut:

```bash
weft deploy --config weft.yaml --target prod --reset-bookmarks
```

Equivalent to `bookmarkMode: clearAll` for this run only.

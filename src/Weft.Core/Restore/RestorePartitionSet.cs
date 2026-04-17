// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AnalysisServices.Tabular;

namespace Weft.Core.Restore;

public sealed class RestorePartitionSet
{
    public IReadOnlyList<string> Compute(BasicRefreshPolicy policy, DateOnly from, DateOnly to)
    {
        if (from > to) throw new ArgumentException("from must be <= to");

        return policy.RollingWindowGranularity switch
        {
            RefreshGranularityType.Year    => Years(from, to),
            RefreshGranularityType.Quarter => Quarters(from, to),
            RefreshGranularityType.Month   => Months(from, to),
            _ => throw new NotSupportedException(
                $"Restore partition set unsupported for granularity {policy.RollingWindowGranularity}")
        };
    }

    private static IReadOnlyList<string> Years(DateOnly from, DateOnly to)
    {
        var list = new List<string>();
        for (var y = from.Year; y <= to.Year; y++) list.Add($"Year{y}");
        return list;
    }

    private static IReadOnlyList<string> Quarters(DateOnly from, DateOnly to)
    {
        var list = new List<string>();
        var start = new DateOnly(from.Year, ((from.Month - 1) / 3) * 3 + 1, 1);
        var cursor = start;
        while (cursor <= to)
        {
            var q = (cursor.Month - 1) / 3 + 1;
            list.Add($"Quarter{cursor.Year}Q{q}");
            cursor = cursor.AddMonths(3);
        }
        return list;
    }

    private static IReadOnlyList<string> Months(DateOnly from, DateOnly to)
    {
        var list = new List<string>();
        var cursor = new DateOnly(from.Year, from.Month, 1);
        while (cursor <= to)
        {
            list.Add($"Month{cursor.Year:D4}-{cursor.Month:D2}");
            cursor = cursor.AddMonths(1);
        }
        return list;
    }
}

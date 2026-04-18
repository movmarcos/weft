// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using Microsoft.AnalysisServices.Tabular;

namespace Weft.Core.RefreshPolicy;

public sealed class RetentionCalculator
{
    private readonly DateOnly _today;

    public RetentionCalculator(DateOnly today) => _today = today;
    public RetentionCalculator() : this(DateOnly.FromDateTime(DateTime.UtcNow)) {}

    public IReadOnlyList<string> PartitionsRemovedBy(
        BasicRefreshPolicy oldPolicy,
        BasicRefreshPolicy newPolicy,
        IEnumerable<string> existingPartitionNames)
    {
        if (newPolicy.RollingWindowPeriods >= oldPolicy.RollingWindowPeriods
            && newPolicy.RollingWindowGranularity == oldPolicy.RollingWindowGranularity)
        {
            return Array.Empty<string>();
        }

        return newPolicy.RollingWindowGranularity switch
        {
            RefreshGranularityType.Year    => YearLoss(newPolicy.RollingWindowPeriods, existingPartitionNames),
            RefreshGranularityType.Quarter => QuarterLoss(newPolicy.RollingWindowPeriods, existingPartitionNames),
            RefreshGranularityType.Month   => MonthLoss(newPolicy.RollingWindowPeriods, existingPartitionNames),
            _ => throw new NotSupportedException(
                $"Granularity {newPolicy.RollingWindowGranularity} not supported by RetentionCalculator.")
        };
    }

    private IReadOnlyList<string> YearLoss(int periods, IEnumerable<string> names)
    {
        var keep = Enumerable.Range(0, periods).Select(i => _today.Year - i).ToHashSet();
        return names
            .Where(n => Regex.IsMatch(n, @"^Year\d{4}$"))
            .Where(n => !keep.Contains(int.Parse(n.AsSpan(4))))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    private IReadOnlyList<string> QuarterLoss(int periods, IEnumerable<string> names)
    {
        var currentQ = (_today.Month - 1) / 3 + 1;
        var kept = new HashSet<(int Y, int Q)>();
        var year = _today.Year;
        var q = currentQ;
        for (int i = 0; i < periods; i++)
        {
            kept.Add((year, q));
            q--;
            if (q == 0) { q = 4; year--; }
        }
        return names
            .Where(n => Regex.IsMatch(n, @"^Quarter(\d{4})Q([1-4])$"))
            .Where(n =>
            {
                var m = Regex.Match(n, @"^Quarter(\d{4})Q([1-4])$");
                return !kept.Contains((int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)));
            })
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    private IReadOnlyList<string> MonthLoss(int periods, IEnumerable<string> names)
    {
        var kept = new HashSet<(int Y, int M)>();
        var cursor = new DateOnly(_today.Year, _today.Month, 1);
        for (int i = 0; i < periods; i++)
        {
            kept.Add((cursor.Year, cursor.Month));
            cursor = cursor.AddMonths(-1);
        }
        return names
            .Where(n => Regex.IsMatch(n, @"^Month(\d{4})-(\d{2})$"))
            .Where(n =>
            {
                var m = Regex.Match(n, @"^Month(\d{4})-(\d{2})$");
                return !kept.Contains((int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)));
            })
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }
}

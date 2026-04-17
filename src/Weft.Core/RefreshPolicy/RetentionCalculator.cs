// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

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
        if (newPolicy.RollingWindowGranularity != RefreshGranularityType.Year)
            throw new NotSupportedException("Only Year granularity supported in v1; extend for Quarter/Month in a follow-up task.");

        // Window did not shrink — no partitions are newly evicted by this change.
        if (newPolicy.RollingWindowPeriods >= oldPolicy.RollingWindowPeriods)
            return Array.Empty<string>();

        var keepYears = Enumerable.Range(0, newPolicy.RollingWindowPeriods)
            .Select(i => _today.Year - i)
            .ToHashSet();

        return existingPartitionNames
            .Where(IsYearPartition)
            .Where(name => !keepYears.Contains(YearOf(name)))
            .OrderBy(x => x)
            .ToList();
    }

    private static bool IsYearPartition(string name) =>
        name.StartsWith("Year", StringComparison.Ordinal) &&
        name.Length == 8 && int.TryParse(name.AsSpan(4), out _);

    private static int YearOf(string name) => int.Parse(name.AsSpan(4));
}

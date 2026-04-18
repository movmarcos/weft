// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.RefreshPolicy;
using Xunit;

namespace Weft.Core.Tests.RefreshPolicy;

public class RetentionCalculatorQuarterMonthTests
{
    private static BasicRefreshPolicy Policy(RefreshGranularityType g, int periods) => new()
    {
        RollingWindowGranularity = g,
        RollingWindowPeriods = periods,
        IncrementalGranularity = RefreshGranularityType.Day,
        IncrementalPeriods = 10,
        SourceExpression = "let s = ... in s"
    };

    [Fact]
    public void Quarter_granularity_lists_partitions_outside_new_window()
    {
        // Today 2026-04-17 => current quarter Q2-2026
        var calc = new RetentionCalculator(today: new DateOnly(2026, 4, 17));
        var lost = calc.PartitionsRemovedBy(
            oldPolicy: Policy(RefreshGranularityType.Quarter, 8),
            newPolicy: Policy(RefreshGranularityType.Quarter, 4),
            existingPartitionNames: new[]
            {
                "Quarter2024Q3", "Quarter2024Q4",
                "Quarter2025Q1", "Quarter2025Q2", "Quarter2025Q3", "Quarter2025Q4",
                "Quarter2026Q1", "Quarter2026Q2"
            });
        // New 4-quarter window ending Q2-2026 keeps Q3-25, Q4-25, Q1-26, Q2-26 → lost: the four older.
        lost.Should().BeEquivalentTo(new[] { "Quarter2024Q3", "Quarter2024Q4", "Quarter2025Q1", "Quarter2025Q2" });
    }

    [Fact]
    public void Month_granularity_lists_partitions_outside_new_window()
    {
        var calc = new RetentionCalculator(today: new DateOnly(2026, 4, 17));
        var lost = calc.PartitionsRemovedBy(
            oldPolicy: Policy(RefreshGranularityType.Month, 24),
            newPolicy: Policy(RefreshGranularityType.Month, 6),
            existingPartitionNames: new[]
            {
                "Month2025-10", "Month2025-11", "Month2025-12",
                "Month2026-01", "Month2026-02", "Month2026-03", "Month2026-04"
            });
        // 6-month window ending Apr-2026 keeps Nov-25..Apr-26. Lost: Oct-25.
        lost.Should().BeEquivalentTo(new[] { "Month2025-10" });
    }
}

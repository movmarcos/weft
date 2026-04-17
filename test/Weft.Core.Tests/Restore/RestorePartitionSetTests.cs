// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Restore;
using Xunit;

namespace Weft.Core.Tests.Restore;

public class RestorePartitionSetTests
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
    public void Year_granularity_returns_year_partition_names()
    {
        var set = new RestorePartitionSet().Compute(
            policy: Policy(RefreshGranularityType.Year, 5),
            from: new DateOnly(2021, 1, 1),
            to:   new DateOnly(2023, 12, 31));

        set.Should().Equal(new[] { "Year2021", "Year2022", "Year2023" });
    }

    [Fact]
    public void Quarter_granularity_returns_quarter_partition_names()
    {
        var set = new RestorePartitionSet().Compute(
            policy: Policy(RefreshGranularityType.Quarter, 8),
            from: new DateOnly(2024, 1, 1),
            to:   new DateOnly(2024, 9, 30));

        set.Should().Equal(new[] { "Quarter2024Q1", "Quarter2024Q2", "Quarter2024Q3" });
    }

    [Fact]
    public void Month_granularity_returns_month_partition_names()
    {
        var set = new RestorePartitionSet().Compute(
            policy: Policy(RefreshGranularityType.Month, 24),
            from: new DateOnly(2025, 11, 1),
            to:   new DateOnly(2026, 1, 31));

        set.Should().Equal(new[] { "Month2025-11", "Month2025-12", "Month2026-01" });
    }
}

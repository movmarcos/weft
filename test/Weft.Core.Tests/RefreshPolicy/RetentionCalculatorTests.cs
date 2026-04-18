// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.RefreshPolicy;
using Xunit;

namespace Weft.Core.Tests.RefreshPolicy;

public class RetentionCalculatorTests
{
    private static BasicRefreshPolicy Policy(int years) => new()
    {
        RollingWindowGranularity = RefreshGranularityType.Year,
        RollingWindowPeriods = years,
        IncrementalGranularity = RefreshGranularityType.Day,
        IncrementalPeriods = 10,
        SourceExpression = "let s = ... in s"
    };

    [Fact]
    public void No_loss_when_window_unchanged()
    {
        var calc = new RetentionCalculator(today: new DateOnly(2026, 4, 17));
        var lost = calc.PartitionsRemovedBy(
            oldPolicy: Policy(5), newPolicy: Policy(5),
            existingPartitionNames: new[] { "Year2021", "Year2022", "Year2023", "Year2024", "Year2025" });
        lost.Should().BeEmpty();
    }

    [Fact]
    public void Lists_partitions_outside_new_window()
    {
        var calc = new RetentionCalculator(today: new DateOnly(2026, 4, 17));
        var lost = calc.PartitionsRemovedBy(
            oldPolicy: Policy(5), newPolicy: Policy(3),
            existingPartitionNames: new[] { "Year2021", "Year2022", "Year2023", "Year2024", "Year2025" });
        lost.Should().BeEquivalentTo(new[] { "Year2021", "Year2022", "Year2023" });
    }
}

// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Diffing;
using Weft.Core.Tests.Fixtures;
using Xunit;

namespace Weft.Core.Tests.Diffing;

public class TableClassifierTests
{
    [Fact]
    public void Static_when_partitions_match_and_no_policy()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");

        var c = new TableClassifier().Classify(
            src.Model.Tables["FactSales"], tgt.Model.Tables["FactSales"]);

        c.Should().Be(TableClassification.Static);
    }

    [Fact]
    public void DynamicallyPartitioned_when_target_has_extra_partitions()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");

        var extra = new Partition
        {
            Name = "FactSales_2024",
            Mode = ModeType.Import,
            Source = new MPartitionSource { Expression = "let s = #table({},{}) in s" }
        };
        tgt.Model.Tables["FactSales"].Partitions.Add(extra);

        var c = new TableClassifier().Classify(
            src.Model.Tables["FactSales"], tgt.Model.Tables["FactSales"]);

        c.Should().Be(TableClassification.DynamicallyPartitioned);
    }

    [Fact]
    public void IncrementalRefreshPolicy_wins_over_dynamic()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");

        src.Model.Tables["FactSales"].RefreshPolicy = new BasicRefreshPolicy
        {
            RollingWindowGranularity = RefreshGranularityType.Year,
            RollingWindowPeriods = 5,
            IncrementalGranularity = RefreshGranularityType.Day,
            IncrementalPeriods = 10,
            SourceExpression = "let s = #table({\"Date\",\"Amount\"}, {}) in s"
        };
        tgt.Model.Tables["FactSales"].Partitions.Add(new Partition
        {
            Name = "FactSales_2024",
            Mode = ModeType.Import,
            Source = new MPartitionSource { Expression = "let s = #table({},{}) in s" }
        });

        var c = new TableClassifier().Classify(
            src.Model.Tables["FactSales"], tgt.Model.Tables["FactSales"]);

        c.Should().Be(TableClassification.IncrementalRefreshPolicy);
    }
}

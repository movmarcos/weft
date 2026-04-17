// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Diffing;
using Weft.Core.Tests.Fixtures;
using Xunit;

namespace Weft.Core.Tests.Diffing;

public class ModelDifferTests
{
    [Fact]
    public void Empty_changeset_when_source_equals_target()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");

        var cs = new ModelDiffer().Compute(src, tgt);

        cs.IsEmpty.Should().BeTrue();
        cs.TablesUnchanged.Should().Contain(new[] { "DimDate", "FactSales" });
    }

    [Fact]
    public void Detects_added_table_in_source()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");
        src.Model.Tables.Add(new Table { Name = "NewTable" });

        var cs = new ModelDiffer().Compute(src, tgt);

        cs.TablesToAdd.Select(t => t.Name).Should().Equal("NewTable");
        cs.TablesToDrop.Should().BeEmpty();
    }

    [Fact]
    public void Detects_dropped_table_when_only_in_target()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");
        tgt.Model.Tables.Add(new Table { Name = "OldTable" });

        var cs = new ModelDiffer().Compute(src, tgt);

        cs.TablesToDrop.Should().Equal("OldTable");
    }

    [Fact]
    public void Column_added_to_existing_table_is_an_alter_with_preserved_partitions()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");

        src.Model.Tables["FactSales"].Columns.Add(
            new DataColumn { Name = "Region", DataType = DataType.String, SourceColumn = "Region" });

        var cs = new ModelDiffer().Compute(src, tgt);

        cs.TablesToAlter.Should().ContainSingle()
            .Which.Should().Match<TableDiff>(d =>
                d.Name == "FactSales" &&
                d.ColumnsAdded.SequenceEqual(new[] { "Region" }) &&
                d.PartitionStrategy == PartitionStrategy.PreserveTarget);
    }

    [Fact]
    public void Refresh_policy_change_marks_table_alter_with_policy_flag()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");

        src.Model.Tables["FactSales"].RefreshPolicy = new BasicRefreshPolicy
        {
            RollingWindowGranularity = RefreshGranularityType.Year,
            RollingWindowPeriods = 5,
            IncrementalGranularity = RefreshGranularityType.Day,
            IncrementalPeriods = 10,
            SourceExpression = "let Source = #table({\"Date\",\"Amount\"}, {}) in Source"
        };

        var cs = new ModelDiffer().Compute(src, tgt);

        var diff = cs.TablesToAlter.Single(d => d.Name == "FactSales");
        diff.RefreshPolicyChanged.Should().BeTrue();
        diff.Classification.Should().Be(TableClassification.IncrementalRefreshPolicy);
    }
}

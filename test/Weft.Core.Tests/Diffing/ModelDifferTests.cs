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
}

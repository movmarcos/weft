// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Diffing;
using Weft.Core.Tmsl;
using Weft.Core.Tests.Fixtures;
using Xunit;

namespace Weft.Core.Tests.Tmsl;

public class TmslBuilderTests
{
    [Fact]
    public void Empty_changeset_produces_an_empty_sequence()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");

        var cs = new ModelDiffer().Compute(src, tgt);
        var json = new TmslBuilder().Build(cs, src, tgt);

        json.Should().Contain("\"sequence\"");
        json.Should().Contain("\"operations\": []");
    }

    [Fact]
    public void Adding_a_table_emits_create_command()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");
        src.Model.Tables.Add(new Table { Name = "NewTable" });

        var cs = new ModelDiffer().Compute(src, tgt);
        var json = new TmslBuilder().Build(cs, src, tgt);

        json.Should().Contain("\"create\"").And.Contain("NewTable");
    }

    [Fact]
    public void Dropping_a_table_emits_delete_command()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");
        tgt.Model.Tables.Add(new Table { Name = "OldTable" });

        var cs = new ModelDiffer().Compute(src, tgt);
        var json = new TmslBuilder().Build(cs, src, tgt);

        json.Should().Contain("\"delete\"").And.Contain("OldTable");
    }

    [Fact]
    public void Altering_a_table_attaches_target_partitions_with_bookmarks()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");

        // Source: add a column. Target: stamp a bookmark annotation on the existing partition.
        src.Model.Tables["FactSales"].Columns.Add(
            new DataColumn { Name = "Region", DataType = DataType.String, SourceColumn = "Region" });
        tgt.Model.Tables["FactSales"].Partitions["FactSales"].Annotations.Add(
            new Annotation { Name = Weft.Core.Partitions.PartitionAnnotationNames.RefreshBookmark, Value = "wm-001" });

        var cs = new ModelDiffer().Compute(src, tgt);
        var json = new TmslBuilder().Build(cs, src, tgt);

        json.Should().Contain("\"createOrReplace\"");
        json.Should().Contain("\"Region\"");
        // Bookmark is preserved as an annotation on the partition, not as a top-level property.
        // Assert the annotation block contains the RefreshBookmark name and the wm-001 value.
        json.Should().Contain("\"name\": \"RefreshBookmark\"");
        json.Should().Contain("\"value\": \"wm-001\"");
    }
}

// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Partitions;
using Weft.Core.Tests.Fixtures;
using Xunit;

namespace Weft.Core.Tests.Partitions;

public class PartitionManifestReaderTests
{
    [Fact]
    public void Captures_all_tables_and_partitions_with_bookmarks()
    {
        var db = FixtureLoader.LoadBim("models/tiny-static.bim");
        // Simulate a bookmark on FactSales partition (stored as an annotation)
        db.Model.Tables["FactSales"].Partitions["FactSales"].Annotations
            .Add(new Annotation { Name = PartitionAnnotationNames.RefreshBookmark, Value = "wm-001" });

        var manifest = new PartitionManifestReader().Read(db);

        manifest.TargetDatabase.Should().Be("TinyStatic");
        manifest.Tables.Should().HaveCount(2);
        manifest.Tables["FactSales"].Should().ContainSingle()
            .Which.RefreshBookmark.Should().Be("wm-001");
        manifest.Tables["DimDate"].Should().ContainSingle()
            .Which.RefreshBookmark.Should().BeNull();
    }
}

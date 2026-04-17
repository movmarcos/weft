// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AnalysisServices.Tabular;
using VerifyXunit;
using Weft.Core.Diffing;
using Weft.Core.Tmsl;
using Weft.Core.Tests.Fixtures;
using Xunit;

namespace Weft.Core.Tests.Tmsl;

public class TmslSnapshotTests
{
    [Fact]
    public Task Snapshot_add_table()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");
        src.Model.Tables.Add(new Table { Name = "AddedTable" });

        var cs = new ModelDiffer().Compute(src, tgt);
        var json = new TmslBuilder().Build(cs, src, tgt);
        return Verifier.VerifyJson(json);
    }

    [Fact]
    public Task Snapshot_alter_with_added_column_preserves_bookmark()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");
        src.Model.Tables["FactSales"].Columns.Add(
            new DataColumn { Name = "Region", DataType = DataType.String, SourceColumn = "Region" });
        // RefreshBookmark in TOM 19.84.1 is stored as an annotation
        tgt.Model.Tables["FactSales"].Partitions["FactSales"].Annotations.Add(
            new Annotation { Name = Weft.Core.Partitions.PartitionAnnotationNames.RefreshBookmark, Value = "wm-001" });

        var cs = new ModelDiffer().Compute(src, tgt);
        var json = new TmslBuilder().Build(cs, src, tgt);
        return Verifier.VerifyJson(json);
    }
}

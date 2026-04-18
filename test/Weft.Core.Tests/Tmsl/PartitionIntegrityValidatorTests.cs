// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Diffing;
using Weft.Core.Tmsl;
using Weft.Core.Tests.Fixtures;
using Xunit;

namespace Weft.Core.Tests.Tmsl;

public class PartitionIntegrityValidatorTests
{
    [Fact]
    public void Passes_when_alter_includes_all_target_partitions()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");
        src.Model.Tables["FactSales"].Columns.Add(
            new DataColumn { Name = "Region", DataType = DataType.String, SourceColumn = "Region" });

        var cs = new ModelDiffer().Compute(src, tgt);
        var json = new TmslBuilder().Build(cs, src, tgt);

        var act = () => new PartitionIntegrityValidator().Validate(json, tgt, cs);
        act.Should().NotThrow();
    }

    [Fact]
    public void Throws_when_alter_drops_a_target_partition()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");
        // Target has a 2nd partition. A buggy alter that omits it should be rejected.
        tgt.Model.Tables["FactSales"].Partitions.Add(new Partition
        {
            Name = "FactSales_2024",
            Mode = ModeType.Import,
            Source = new MPartitionSource { Expression = "let s = #table({},{}) in s" }
        });

        var malicious = """
        {
          "sequence": {
            "maxParallelism": 1,
            "operations": [
              {
                "createOrReplace": {
                  "object": { "database": "TinyStatic", "table": "FactSales" },
                  "table": {
                    "name": "FactSales",
                    "columns": [],
                    "partitions": [
                      { "name": "FactSales", "mode": "import", "source": { "type": "m", "expression": "x" } }
                    ]
                  }
                }
              }
            ]
          }
        }
        """;

        var cs = new ChangeSet(
            TablesToAdd: Array.Empty<TablePlan>(),
            TablesToDrop: Array.Empty<string>(),
            TablesToAlter: Array.Empty<TableDiff>(),
            TablesUnchanged: Array.Empty<string>(),
            MeasuresChanged: Array.Empty<string>(),
            RelationshipsChanged: Array.Empty<string>(),
            RolesChanged: Array.Empty<string>(),
            PerspectivesChanged: Array.Empty<string>(),
            CulturesChanged: Array.Empty<string>(),
            ExpressionsChanged: Array.Empty<string>(),
            DataSourcesChanged: Array.Empty<string>());

        var act = () => new PartitionIntegrityValidator().Validate(malicious, tgt, cs);
        act.Should().Throw<PartitionIntegrityException>()
           .WithMessage("*FactSales_2024*");
    }

    [Fact]
    public void Throws_when_bookmark_is_dropped_during_preservation()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");
        // Target partition has a bookmark.
        tgt.Model.Tables["FactSales"].Partitions["FactSales"].Annotations.Add(
            new Annotation { Name = Weft.Core.Partitions.PartitionAnnotationNames.RefreshBookmark, Value = "wm-001" });

        // Malicious TMSL: same partition name retained but the RefreshBookmark annotation is missing.
        var malicious = """
        {
          "sequence": {
            "maxParallelism": 1,
            "operations": [
              {
                "createOrReplace": {
                  "object": { "database": "TinyStatic", "table": "FactSales" },
                  "table": {
                    "name": "FactSales",
                    "columns": [],
                    "partitions": [
                      { "name": "FactSales", "mode": "import", "source": { "type": "m", "expression": "x" } }
                    ]
                  }
                }
              }
            ]
          }
        }
        """;

        var cs = new ChangeSet(
            TablesToAdd: Array.Empty<TablePlan>(),
            TablesToDrop: Array.Empty<string>(),
            TablesToAlter: Array.Empty<TableDiff>(),
            TablesUnchanged: Array.Empty<string>(),
            MeasuresChanged: Array.Empty<string>(),
            RelationshipsChanged: Array.Empty<string>(),
            RolesChanged: Array.Empty<string>(),
            PerspectivesChanged: Array.Empty<string>(),
            CulturesChanged: Array.Empty<string>(),
            ExpressionsChanged: Array.Empty<string>(),
            DataSourcesChanged: Array.Empty<string>());

        var act = () => new PartitionIntegrityValidator().Validate(malicious, tgt, cs);
        act.Should().Throw<PartitionIntegrityException>()
           .WithMessage("*Bookmark*");
    }

    [Fact]
    public void Throws_when_emitted_bookmark_appears_on_partition_without_one_on_target()
    {
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");
        // target has NO bookmark on FactSales partition.

        var malicious = """
        {
          "sequence": {
            "maxParallelism": 1,
            "operations": [
              {
                "createOrReplace": {
                  "object": { "database": "TinyStatic", "table": "FactSales" },
                  "table": {
                    "name": "FactSales",
                    "columns": [],
                    "partitions": [
                      {
                        "name": "FactSales", "mode": "import",
                        "source": { "type": "m", "expression": "x" },
                        "annotations": [ { "name": "RefreshBookmark", "value": "injected" } ]
                      }
                    ]
                  }
                }
              }
            ]
          }
        }
        """;

        var cs = new ChangeSet(
            TablesToAdd: Array.Empty<TablePlan>(),
            TablesToDrop: Array.Empty<string>(),
            TablesToAlter: Array.Empty<TableDiff>(),
            TablesUnchanged: Array.Empty<string>(),
            MeasuresChanged: Array.Empty<string>(),
            RelationshipsChanged: Array.Empty<string>(),
            RolesChanged: Array.Empty<string>(),
            PerspectivesChanged: Array.Empty<string>(),
            CulturesChanged: Array.Empty<string>(),
            ExpressionsChanged: Array.Empty<string>(),
            DataSourcesChanged: Array.Empty<string>());

        var act = () => new PartitionIntegrityValidator().Validate(malicious, tgt, cs);
        act.Should().Throw<PartitionIntegrityException>().WithMessage("*injected*");
    }
}

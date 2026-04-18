// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Partitions;
using Weft.Xmla;
using Xunit;

namespace Weft.Xmla.Tests;

public class BookmarkClearerTests
{
    [Fact]
    public void Emits_sequence_that_clears_bookmark_annotations_on_named_tables()
    {
        var db = new Database { Name = "D", CompatibilityLevel = 1600 };
        db.Model = new Model();
        var t = new Table { Name = "FactSales" };
        var p = new Partition
        {
            Name = "FactSales",
            Mode = ModeType.Import,
            Source = new MPartitionSource { Expression = "let s = #table({},{}) in s" }
        };
        p.Annotations.Add(new Annotation { Name = PartitionAnnotationNames.RefreshBookmark, Value = "wm-001" });
        t.Partitions.Add(p);
        db.Model.Tables.Add(t);

        var json = new BookmarkClearer().BuildTmsl(db, new[] { "FactSales" });

        json.Should().Contain("\"delete\"");
        json.Should().Contain("\"FactSales\"");
        json.Should().Contain("\"annotation\"");
        json.Should().Contain("\"RefreshBookmark\"");
    }

    [Fact]
    public void Emits_empty_sequence_when_no_bookmarks_to_clear()
    {
        var db = new Database { Name = "D", CompatibilityLevel = 1600 };
        db.Model = new Model();
        var t = new Table { Name = "FactSales" };
        t.Partitions.Add(new Partition
        {
            Name = "FactSales",
            Mode = ModeType.Import,
            Source = new MPartitionSource { Expression = "x" }
        });
        db.Model.Tables.Add(t);

        var json = new BookmarkClearer().BuildTmsl(db, new[] { "FactSales" });

        json.Should().Contain("\"operations\": []");
    }
}

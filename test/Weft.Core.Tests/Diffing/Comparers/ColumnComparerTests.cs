// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Diffing.Comparers;
using Xunit;

namespace Weft.Core.Tests.Diffing.Comparers;

public class ColumnComparerTests
{
    private static DataColumn Col(string name, DataType dt = DataType.String, string src = "x") =>
        new() { Name = name, DataType = dt, SourceColumn = src };

    [Fact]
    public void Detects_added_removed_modified()
    {
        var sourceTable = new Table();
        sourceTable.Columns.Add(Col("A"));
        sourceTable.Columns.Add(Col("B", DataType.Int64));
        sourceTable.Columns.Add(Col("C"));

        var targetTable = new Table();
        targetTable.Columns.Add(Col("A"));
        targetTable.Columns.Add(Col("B", DataType.String));
        targetTable.Columns.Add(Col("D"));

        var d = new ColumnComparer().Compare(sourceTable.Columns, targetTable.Columns);

        d.Added.Should().Equal("C");
        d.Removed.Should().Equal("D");
        d.Modified.Should().Equal("B");
    }
}

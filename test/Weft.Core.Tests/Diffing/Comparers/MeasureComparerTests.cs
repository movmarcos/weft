// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Diffing.Comparers;
using Xunit;

namespace Weft.Core.Tests.Diffing.Comparers;

public class MeasureComparerTests
{
    [Fact]
    public void Detects_added_removed_modified_measures()
    {
        var src = new Table();
        src.Measures.Add(new Measure { Name = "A", Expression = "1" });
        src.Measures.Add(new Measure { Name = "B", Expression = "2" });

        var tgt = new Table();
        tgt.Measures.Add(new Measure { Name = "A", Expression = "1" });
        tgt.Measures.Add(new Measure { Name = "B", Expression = "999" });
        tgt.Measures.Add(new Measure { Name = "Z", Expression = "0" });

        var d = new MeasureComparer().Compare(src.Measures, tgt.Measures);

        d.Added.Should().BeEmpty();
        d.Removed.Should().Equal("Z");
        d.Modified.Should().Equal("B");
    }
}

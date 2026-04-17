// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Xunit;

namespace Weft.Core.Tests.Fixtures;

public class FixtureLoaderTests
{
    [Fact]
    public void Loads_tiny_static_with_two_tables()
    {
        var db = FixtureLoader.LoadBim("models/tiny-static.bim");
        db.Name.Should().Be("TinyStatic");
        db.Model.Tables.Should().HaveCount(2);
        db.Model.Tables["FactSales"].Measures.Should().ContainSingle(m => m.Name == "Total Sales");
    }
}

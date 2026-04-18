// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Weft.Core.Loading;
using Weft.Core.Tests.Fixtures;
using Xunit;

namespace Weft.Core.Tests.Loading;

public class BimFileLoaderTests
{
    [Fact]
    public void Loads_a_bim_file_into_a_TOM_database()
    {
        var path = FixtureLoader.FixturePath("models", "tiny-static.bim");
        var loader = new BimFileLoader();

        var db = loader.Load(path);

        db.Name.Should().Be("TinyStatic");
        db.Model.Tables.Should().HaveCount(2);
    }

    [Fact]
    public void Throws_on_missing_file()
    {
        var loader = new BimFileLoader();
        var act = () => loader.Load("/no/such/path.bim");
        act.Should().Throw<FileNotFoundException>();
    }
}

// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Weft.Core.Loading;
using Weft.Core.Tests.Fixtures;
using Xunit;

namespace Weft.Core.Tests.Loading;

public class TabularEditorFolderLoaderTests
{
    [Fact]
    public void Stitches_folder_into_a_TOM_database()
    {
        var dir = FixtureLoader.FixturePath("models", "tiny-folder");
        var loader = new TabularEditorFolderLoader();

        var db = loader.Load(dir);

        db.Name.Should().Be("TinyFolder");
        db.Model.Tables.Select(t => t.Name).Should().BeEquivalentTo(new[] { "DimDate", "FactSales" });
        db.Model.Tables["FactSales"].Measures.Should().ContainSingle(m => m.Name == "Total Sales");
    }

    [Fact]
    public void Throws_on_missing_database_json()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var loader = new TabularEditorFolderLoader();
            var act = () => loader.Load(dir);
            act.Should().Throw<FileNotFoundException>().WithMessage("*database.json*");
        }
        finally { Directory.Delete(dir); }
    }
}

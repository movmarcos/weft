// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using WeftStudio.App;
using WeftStudio.Ui.Explorer;

namespace WeftStudio.Ui.Tests;

public class ExplorerViewModelTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim");

    [Fact]
    public void Root_shows_Tables_and_Measures_categories()
    {
        var s = ModelSession.OpenBim(FixturePath);
        var vm = new ExplorerViewModel(s);

        vm.Roots.Select(r => r.DisplayName)
            .Should().Contain(new[] { "Tables", "Measures", "Relationships" });
    }

    [Fact]
    public void Tables_node_lists_each_table()
    {
        var s = ModelSession.OpenBim(FixturePath);
        var vm = new ExplorerViewModel(s);

        var tablesNode = vm.Roots.Single(r => r.DisplayName == "Tables");
        tablesNode.Children.Should().NotBeEmpty();
        tablesNode.Children.Select(c => c.DisplayName).Should().Contain("FactSales");
    }

    [Fact]
    public void Measures_node_flattens_all_measures_across_tables()
    {
        var s = ModelSession.OpenBim(FixturePath);
        var vm = new ExplorerViewModel(s);

        var measuresNode = vm.Roots.Single(r => r.DisplayName == "Measures");
        measuresNode.Children.Should().NotBeEmpty();
    }
}

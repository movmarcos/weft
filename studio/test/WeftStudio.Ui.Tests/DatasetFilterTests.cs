// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using NSubstitute;
using WeftStudio.App.Connections;
using WeftStudio.Ui.Connect;

namespace WeftStudio.Ui.Tests;

public class DatasetFilterTests
{
    private static ConnectDialogViewModel VmWithDatasets(params string[] names)
    {
        var mgr = Substitute.For<IConnectionManager>();
        var vm = new ConnectDialogViewModel(mgr);
        foreach (var n in names)
            vm.Datasets.Add(new DatasetRow(new DatasetInfo(n, null, null, null, null)));
        return vm;
    }

    [Fact]
    public void Empty_filter_shows_all()
    {
        var vm = VmWithDatasets("alpha", "beta", "gamma");
        vm.FilterText = "";
        vm.VisibleDatasets.Should().HaveCount(3);
    }

    [Fact]
    public void Filter_is_substring_case_insensitive()
    {
        var vm = VmWithDatasets("Sales_Fact", "Inventory", "sales_targets");
        vm.FilterText = "sales";
        vm.VisibleDatasets.Select(d => d.Name)
            .Should().Equal("Sales_Fact", "sales_targets");
    }
}

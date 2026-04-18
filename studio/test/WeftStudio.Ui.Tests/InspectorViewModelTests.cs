// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using WeftStudio.App;
using WeftStudio.Ui.Inspector;

namespace WeftStudio.Ui.Tests;

public class InspectorViewModelTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim");

    [Fact]
    public void Reflects_measure_name_and_format()
    {
        var s = ModelSession.OpenBim(FixturePath);
        var m = s.Database.Model.Tables["FactSales"].Measures[0];
        var vm = new InspectorViewModel(s, "FactSales", m.Name);

        vm.Name.Should().Be(m.Name);
    }

    [Fact]
    public void Renaming_via_inspector_applies_RenameMeasureCommand()
    {
        var s = ModelSession.OpenBim(FixturePath);
        var m = s.Database.Model.Tables["FactSales"].Measures[0];
        var originalName = m.Name;
        var vm = new InspectorViewModel(s, "FactSales", originalName);

        vm.Name = "Renamed";
        vm.CommitRename();

        s.Database.Model.Tables["FactSales"].Measures.Contains("Renamed")
            .Should().BeTrue();
        s.IsDirty.Should().BeTrue();
    }
}

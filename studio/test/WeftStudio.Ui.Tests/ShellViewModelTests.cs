// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using WeftStudio.App;
using WeftStudio.Ui.DaxEditor;
using WeftStudio.Ui.Shell;

namespace WeftStudio.Ui.Tests;

public class ShellViewModelTests
{
    [Fact]
    public void Defaults_to_explorer_mode()
    {
        var vm = new ShellViewModel();
        vm.ActiveMode.Should().Be(ActivityMode.Explorer);
    }

    [Fact]
    public void SwitchTo_changes_active_mode_and_raises_property_changed()
    {
        var vm = new ShellViewModel();
        var raised = false;
        vm.PropertyChanged += (_, e) =>
            { if (e.PropertyName == nameof(vm.ActiveMode)) raised = true; };

        vm.ActiveMode = ActivityMode.Diagram;

        vm.ActiveMode.Should().Be(ActivityMode.Diagram);
        raised.Should().BeTrue();
    }

    [Fact]
    public void OpenMeasure_adds_tab_and_activates_it()
    {
        var fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim");
        var vm = new ShellViewModel();
        vm.OpenModel(fixture);

        var measure = vm.Explorer!.Session.Database.Model.Tables["FactSales"].Measures[0];
        vm.OpenMeasure("FactSales", measure.Name);

        vm.OpenTabs.Should().ContainSingle()
            .Which.MeasureName.Should().Be(measure.Name);
        vm.ActiveTab.Should().Be(vm.OpenTabs[0]);
    }

    [Fact]
    public void OpenMeasure_focuses_existing_tab_if_already_open()
    {
        var fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim");
        var vm = new ShellViewModel();
        vm.OpenModel(fixture);
        var measure = vm.Explorer!.Session.Database.Model.Tables["FactSales"].Measures[0];
        vm.OpenMeasure("FactSales", measure.Name);
        vm.OpenMeasure("FactSales", measure.Name);

        vm.OpenTabs.Should().ContainSingle();
    }
}

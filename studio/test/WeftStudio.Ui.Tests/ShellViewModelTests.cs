// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
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
}

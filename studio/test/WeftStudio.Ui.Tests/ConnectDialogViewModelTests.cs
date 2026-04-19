// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using NSubstitute;
using WeftStudio.App.Connections;
using WeftStudio.Ui.Connect;

namespace WeftStudio.Ui.Tests;

public class ConnectDialogViewModelTests
{
    private static ConnectDialogViewModel NewVm() =>
        new(Substitute.For<IConnectionManager>());

    [Fact]
    public void Starts_in_Idle_state()
    {
        var vm = NewVm();
        vm.State.Should().Be(ConnectDialogState.Idle);
        vm.UrlError.Should().BeNull();
    }

    [Fact]
    public void Setting_valid_Url_transitions_to_Ready()
    {
        var vm = NewVm();
        vm.Url = "powerbi://api.powerbi.com/v1.0/myorg/X";
        vm.State.Should().Be(ConnectDialogState.Ready);
        vm.UrlError.Should().BeNull();
    }

    [Fact]
    public void Setting_invalid_Url_stays_Idle_with_error()
    {
        var vm = NewVm();
        vm.Url = "nope";
        vm.State.Should().Be(ConnectDialogState.Idle);
        vm.UrlError.Should().NotBeNull();
    }

    [Fact]
    public void Clearing_Url_returns_to_Idle()
    {
        var vm = NewVm();
        vm.Url = "powerbi://api.powerbi.com/v1.0/myorg/X";
        vm.Url = "";
        vm.State.Should().Be(ConnectDialogState.Idle);
    }
}

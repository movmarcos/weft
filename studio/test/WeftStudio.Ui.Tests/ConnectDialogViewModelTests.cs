// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using NSubstitute;
using Weft.Auth;
using Weft.Core.Abstractions;
using WeftStudio.App;
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

    [Fact]
    public async Task SignIn_success_populates_Datasets_and_moves_to_Picker()
    {
        var mgr = Substitute.For<IConnectionManager>();
        mgr.SignInAsync(Arg.Any<AuthOptions>(), Arg.Any<CancellationToken>())
            .Returns(new AccessToken("jwt", DateTimeOffset.UtcNow.AddHours(1)));
        mgr.ListDatasetsAsync(Arg.Any<WorkspaceReference>(), Arg.Any<AccessToken>(), Arg.Any<CancellationToken>())
            .Returns(new[] { new DatasetInfo("DS1", null, null, null, null),
                             new DatasetInfo("DS2", null, null, null, null) });

        var vm = new ConnectDialogViewModel(mgr) { Url = "powerbi://api.powerbi.com/v1.0/myorg/X" };
        vm.ClientId = "some-client-id";

        await vm.SignInAsync();

        vm.State.Should().Be(ConnectDialogState.Picker);
        vm.Datasets.Should().HaveCount(2);
        vm.ErrorBanner.Should().BeNull();
    }

    [Fact]
    public async Task SignIn_failure_sets_ErrorBanner_and_returns_to_Ready()
    {
        var mgr = Substitute.For<IConnectionManager>();
        mgr.SignInAsync(Arg.Any<AuthOptions>(), Arg.Any<CancellationToken>())
            .Returns<Task<AccessToken>>(_ => throw new InvalidOperationException("AADSTS50020 no user"));

        var vm = new ConnectDialogViewModel(mgr) { Url = "powerbi://api.powerbi.com/v1.0/myorg/X" };
        vm.ClientId = "some-client-id";

        await vm.SignInAsync();

        vm.ErrorBanner.Should().Contain("AADSTS50020");
        vm.State.Should().Be(ConnectDialogState.Ready);
    }

    [Fact]
    public async Task Open_selected_returns_ReadOnly_session()
    {
        var mgr = Substitute.For<IConnectionManager>();
        mgr.SignInAsync(Arg.Any<AuthOptions>(), Arg.Any<CancellationToken>())
            .Returns(new AccessToken("jwt", DateTimeOffset.UtcNow.AddHours(1)));
        mgr.ListDatasetsAsync(Arg.Any<WorkspaceReference>(), Arg.Any<AccessToken>(), Arg.Any<CancellationToken>())
            .Returns(new[] { new DatasetInfo("DS1", null, null, null, null) });

        var fakeSession = ModelSession.OpenBim(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim"));
        var readOnly = new ModelSession(fakeSession.Database, null, readOnly: true);
        mgr.FetchModelAsync(Arg.Any<WorkspaceReference>(), Arg.Any<DatasetInfo>(),
                           Arg.Any<AccessToken>(), Arg.Any<CancellationToken>())
            .Returns(readOnly);

        var vm = new ConnectDialogViewModel(mgr) { Url = "powerbi://api.powerbi.com/v1.0/myorg/X" };
        vm.ClientId = "some-client-id";
        await vm.SignInAsync();
        vm.SelectedRow = vm.Datasets[0];

        var result = await vm.OpenAsync();

        result.Should().NotBeNull();
        result!.ReadOnly.Should().BeTrue();
    }
}

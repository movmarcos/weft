// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Reactive.Linq;
using FluentAssertions;
using NSubstitute;
using Weft.Auth;
using Weft.Core.Abstractions;
using WeftStudio.App;
using WeftStudio.App.Connections;
using WeftStudio.Ui.Connect;
using WeftStudio.Ui.Shell;

namespace WeftStudio.Ui.Tests;

public class WorkspaceOpenSmokeTests
{
    [Fact]
    public async Task Connect_dialog_VM_produces_ReadOnly_session_that_shell_adopts()
    {
        // Arrange: fake connection manager.
        var fakeDb = new Weft.Core.Loading.BimFileLoader().Load(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim"));
        var mgr = Substitute.For<IConnectionManager>();
        mgr.SignInAsync(Arg.Any<AuthOptions>(), Arg.Any<CancellationToken>())
            .Returns(new AccessToken("jwt", DateTimeOffset.UtcNow.AddHours(1)));
        mgr.ListDatasetsAsync(Arg.Any<WorkspaceReference>(), Arg.Any<AccessToken>(), Arg.Any<CancellationToken>())
            .Returns(new[] { new DatasetInfo("Sales", null, null, null, null) });
        mgr.FetchModelAsync(Arg.Any<WorkspaceReference>(), Arg.Any<DatasetInfo>(),
                           Arg.Any<AccessToken>(), Arg.Any<CancellationToken>())
            .Returns(new ModelSession(fakeDb, sourcePath: null, readOnly: true));

        // Act: drive the VM through its full state machine.
        var vm = new ConnectDialogViewModel(mgr)
        {
            Url = "powerbi://api.powerbi.com/v1.0/myorg/DEV",
            ClientId = "some-client-id",
        };
        await vm.SignInAsync();
        vm.SelectedRow = vm.VisibleDatasets[0];
        var session = await vm.OpenAsync();

        // Assert: session is read-only.
        session.Should().NotBeNull();
        session!.ReadOnly.Should().BeTrue();

        // Shell adopts it and Save is disabled.
        var shell = new ShellViewModel();
        shell.AdoptSession(session, workspaceLabel: "DEV / Sales");
        shell.IsReadOnly.Should().BeTrue();
        shell.WorkspaceLabel.Should().Be("DEV / Sales");
        (await shell.SaveCommand.CanExecute.FirstAsync()).Should().BeFalse();
    }
}

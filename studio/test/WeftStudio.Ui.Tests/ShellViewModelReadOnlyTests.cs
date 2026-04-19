// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Reactive.Linq;
using FluentAssertions;
using WeftStudio.App;
using WeftStudio.Ui.Shell;

namespace WeftStudio.Ui.Tests;

public class ShellViewModelReadOnlyTests
{
    [Fact]
    public async Task Workspace_session_disables_SaveCommand()
    {
        var s = ModelSession.OpenBim(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim"));
        var readOnlySession = new ModelSession(s.Database, sourcePath: null, readOnly: true);

        var vm = new ShellViewModel();
        vm.AdoptSession(readOnlySession);

        vm.IsReadOnly.Should().BeTrue();
        (await vm.SaveCommand.CanExecute.FirstAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Bim_session_keeps_SaveCommand_enabled()
    {
        var fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim");
        var vm = new ShellViewModel();
        vm.OpenModel(fixture);

        vm.IsReadOnly.Should().BeFalse();
        (await vm.SaveCommand.CanExecute.FirstAsync()).Should().BeTrue();
    }

    [Fact]
    public void WorkspaceLabel_reflects_session_source()
    {
        var s = ModelSession.OpenBim(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim"));
        var readOnlySession = new ModelSession(s.Database, sourcePath: null, readOnly: true);

        var vm = new ShellViewModel();
        vm.AdoptSession(readOnlySession, workspaceLabel: "DEV / SalesDS");

        vm.WorkspaceLabel.Should().Be("DEV / SalesDS");
    }
}

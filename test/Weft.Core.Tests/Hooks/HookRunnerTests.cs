// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Weft.Core.Hooks;
using Xunit;

namespace Weft.Core.Tests.Hooks;

public class HookRunnerTests
{
    private static HookContext MakeContext() => new(
        ProfileName: "test",
        WorkspaceUrl: "powerbi://x",
        DatabaseName: "D",
        Phase: HookPhase.PreDeploy,
        ChangeSet: new ChangeSetSnapshot(
            Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<string>(), Array.Empty<string>()));

    [Fact]
    public async Task Runs_a_shell_command_and_captures_exit_code()
    {
        if (OperatingSystem.IsWindows()) return;
        var runner = new HookRunner();
        var result = await runner.RunAsync(new HookDefinition(HookPhase.PreDeploy, "true"), MakeContext());
        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task Nonzero_exit_is_surfaced_but_does_not_throw()
    {
        if (OperatingSystem.IsWindows()) return;
        var runner = new HookRunner();
        var result = await runner.RunAsync(new HookDefinition(HookPhase.PreDeploy, "false"), MakeContext());
        result.ExitCode.Should().NotBe(0);
    }
}

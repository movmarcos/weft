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

    [Fact]
    public async Task Scrubs_secret_env_vars_from_child_process()
    {
        if (OperatingSystem.IsWindows()) return;

        Environment.SetEnvironmentVariable("WEFT_CLIENT_SECRET", "super-secret-value");
        try
        {
            var runner = new HookRunner();
            var ctx = new HookContext("t", "x", "D", HookPhase.PreDeploy,
                new ChangeSetSnapshot(Array.Empty<string>(), Array.Empty<string>(),
                    Array.Empty<string>(), Array.Empty<string>()));

            // `/usr/bin/env` lists all env vars; if scrubbing works, WEFT_CLIENT_SECRET is absent.
            var result = await runner.RunAsync(
                new HookDefinition(HookPhase.PreDeploy, "/usr/bin/env"), ctx);
            result.ExitCode.Should().Be(0);
            result.Stdout.Should().NotContain("super-secret-value");
        }
        finally { Environment.SetEnvironmentVariable("WEFT_CLIENT_SECRET", null); }
    }
}

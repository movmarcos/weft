// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Weft.Cli.Commands;

namespace Weft.Cli.Tests;

public class PlanCommandTests
{
    [Fact]
    public async Task Writes_plan_artifact_and_returns_zero()
    {
        var fixture = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "test", "Weft.Core.Tests", "fixtures", "models", "tiny-static.bim");
        var artifactsDir = Directory.CreateTempSubdirectory().FullName;

        try
        {
            var exit = await PlanCommand.RunAsync(
                source: fixture,
                targetSnapshot: fixture,        // self-compare → empty plan
                artifactsDirectory: artifactsDir);

            exit.Should().Be(ExitCodes.Success);
            Directory.GetFiles(artifactsDir, "*-plan.tmsl").Should().NotBeEmpty();
        }
        finally { Directory.Delete(artifactsDir, recursive: true); }
    }
}

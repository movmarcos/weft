// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Weft.Cli.Commands;

namespace Weft.Cli.Tests;

public class ValidateCommandTests
{
    [Fact]
    public async Task Returns_zero_on_valid_bim()
    {
        var fixture = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "test", "Weft.Core.Tests", "fixtures", "models", "tiny-static.bim");

        var exit = await ValidateCommand.RunAsync(fixture);
        exit.Should().Be(0);
    }

    [Fact]
    public async Task Returns_SourceLoadError_on_missing_file()
    {
        var exit = await ValidateCommand.RunAsync("/no/such/path.bim");
        exit.Should().Be(ExitCodes.SourceLoadError);
    }
}

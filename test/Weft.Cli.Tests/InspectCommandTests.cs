// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Weft.Cli.Commands;

namespace Weft.Cli.Tests;

public class InspectCommandTests
{
    [Fact]
    public async Task Lists_partitions_from_a_bim_snapshot()
    {
        var fixture = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "test", "Weft.Core.Tests", "fixtures", "models", "tiny-static.bim");

        var sw = new StringWriter();
        var prevOut = Console.Out;
        Console.SetOut(sw);
        try
        {
            var exit = await InspectCommand.RunFromSnapshotAsync(fixture, tableFilter: null);
            var output = sw.ToString();

            exit.Should().Be(ExitCodes.Success);
            output.Should().Contain("FactSales");
            output.Should().Contain("DimDate");
        }
        finally
        {
            Console.SetOut(prevOut);
        }
    }
}

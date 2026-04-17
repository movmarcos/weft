// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Weft.Cli;

namespace Weft.Cli.Tests;

public class ExitCodesTests
{
    [Fact]
    public void Codes_match_spec()
    {
        ExitCodes.Success.Should().Be(0);
        ExitCodes.ConfigError.Should().Be(2);
        ExitCodes.AuthError.Should().Be(3);
        ExitCodes.SourceLoadError.Should().Be(4);
        ExitCodes.TargetReadError.Should().Be(5);
        ExitCodes.DiffValidationError.Should().Be(6);
        ExitCodes.TmslExecutionError.Should().Be(7);
        ExitCodes.RefreshError.Should().Be(8);
        ExitCodes.PartitionIntegrityError.Should().Be(9);
    }
}

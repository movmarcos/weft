// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Weft.Config;

namespace Weft.Config.Tests;

public class EnvVarExpanderTests
{
    [Fact]
    public void Expands_dollar_brace_references_in_strings()
    {
        try
        {
            Environment.SetEnvironmentVariable("WEFT_TEST_TENANT", "tenant-123");
            var input = "${WEFT_TEST_TENANT}";
            EnvVarExpander.Expand(input).Should().Be("tenant-123");
        }
        finally { Environment.SetEnvironmentVariable("WEFT_TEST_TENANT", null); }
    }

    [Fact]
    public void Leaves_unreferenced_strings_alone()
    {
        EnvVarExpander.Expand("plain-text").Should().Be("plain-text");
        EnvVarExpander.Expand(null).Should().BeNull();
    }

    [Fact]
    public void Throws_when_referenced_variable_missing()
    {
        Environment.SetEnvironmentVariable("WEFT_TEST_MISSING", null);
        var act = () => EnvVarExpander.Expand("${WEFT_TEST_MISSING}");
        act.Should().Throw<WeftConfigValidationException>().WithMessage("*WEFT_TEST_MISSING*");
    }

    [Fact]
    public void Expands_multiple_references_in_one_string()
    {
        try
        {
            Environment.SetEnvironmentVariable("WEFT_A", "alpha");
            Environment.SetEnvironmentVariable("WEFT_B", "beta");
            EnvVarExpander.Expand("${WEFT_A}-${WEFT_B}").Should().Be("alpha-beta");
        }
        finally
        {
            Environment.SetEnvironmentVariable("WEFT_A", null);
            Environment.SetEnvironmentVariable("WEFT_B", null);
        }
    }
}

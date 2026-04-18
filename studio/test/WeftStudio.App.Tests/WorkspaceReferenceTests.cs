// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using WeftStudio.App.Connections;

namespace WeftStudio.App.Tests;

public class WorkspaceReferenceTests
{
    [Fact]
    public void Parse_accepts_powerbi_fabric_url_and_extracts_workspace_name()
    {
        var r = WorkspaceReference.Parse("powerbi://api.powerbi.com/v1.0/myorg/DEV - Finance");
        r.Server.Should().Be("powerbi://api.powerbi.com/v1.0/myorg/DEV - Finance");
        r.WorkspaceName.Should().Be("DEV - Finance");
    }

    [Fact]
    public void Parse_accepts_asazure_url_and_leaves_workspace_name_empty()
    {
        var r = WorkspaceReference.Parse("asazure://westeurope.asazure.windows.net/my-aas");
        r.Server.Should().Be("asazure://westeurope.asazure.windows.net/my-aas");
        r.WorkspaceName.Should().Be("");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("http://api.powerbi.com/v1.0/myorg/foo")]
    [InlineData("not-a-url")]
    public void Parse_rejects_malformed_input(string url)
    {
        Action act = () => WorkspaceReference.Parse(url);
        act.Should().Throw<WorkspaceUrlException>();
    }

    [Fact]
    public void Parse_trims_trailing_whitespace()
    {
        var r = WorkspaceReference.Parse("  powerbi://api.powerbi.com/v1.0/myorg/X  ");
        r.Server.Should().Be("powerbi://api.powerbi.com/v1.0/myorg/X");
    }
}

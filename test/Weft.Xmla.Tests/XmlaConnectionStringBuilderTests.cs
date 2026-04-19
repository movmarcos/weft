// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Weft.Core.Abstractions;
using Weft.Xmla;

namespace Weft.Xmla.Tests;

public class XmlaConnectionStringBuilderTests
{
    private static readonly AccessToken FakeToken =
        new("eyJhbGciOiJSUzI1NiJ9.fake.token", DateTimeOffset.UtcNow.AddHours(1));

    [Fact]
    public void Build_includes_data_source_initial_catalog_and_password()
    {
        var conn = new XmlaConnectionStringBuilder()
            .Build("powerbi://api.powerbi.com/v1.0/myorg/Weft-Dev", "SalesModel", FakeToken);

        conn.Should().Contain("Data Source=powerbi://api.powerbi.com/v1.0/myorg/Weft-Dev");
        conn.Should().Contain("Initial Catalog=SalesModel");
        conn.Should().Contain($"Password={FakeToken.Value}");
        conn.Should().NotContain("Provider=MSOLAP");
    }

    [Fact]
    public void BuildServerOnly_omits_initial_catalog()
    {
        var conn = new XmlaConnectionStringBuilder()
            .BuildServerOnly("powerbi://api.powerbi.com/v1.0/myorg/Weft-Dev", FakeToken);

        conn.Should().Contain("Data Source=powerbi://api.powerbi.com/v1.0/myorg/Weft-Dev");
        conn.Should().Contain($"Password={FakeToken.Value}");
        conn.Should().NotContain("Initial Catalog");
    }

    [Fact]
    public void Build_throws_when_token_value_is_blank()
    {
        var blank = new AccessToken("", DateTimeOffset.UtcNow.AddHours(1));

        var act = () => new XmlaConnectionStringBuilder()
            .Build("powerbi://x/y/z/w", "DB", blank);

        act.Should().Throw<ArgumentException>();
    }
}

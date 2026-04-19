// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Weft.Xmla;

namespace Weft.Xmla.Tests;

public class XmlaConnectionStringBuilderTests
{
    [Fact]
    public void Builds_connection_string_for_powerbi_premium_workspace()
    {
        var conn = new XmlaConnectionStringBuilder()
            .Build("powerbi://api.powerbi.com/v1.0/myorg/Weft-Dev", "SalesModel");

        conn.Should().Contain("Provider=MSOLAP");
        conn.Should().Contain("Data Source=powerbi://api.powerbi.com/v1.0/myorg/Weft-Dev");
        conn.Should().Contain("Initial Catalog=SalesModel");
    }
}

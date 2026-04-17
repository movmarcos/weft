// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Weft.Xmla;

namespace Weft.Xmla.Tests;

public class RefreshCommandBuilderTests
{
    [Fact]
    public void Builds_full_refresh_for_one_table()
    {
        var json = new RefreshCommandBuilder().Build(
            databaseName: "TinyStatic",
            entries: new[]
            {
                new RefreshTableEntry("FactSales",
                    new RefreshTypeSpec(RefreshTypeSpec.RefreshKind.Full, ApplyRefreshPolicy: false))
            },
            effectiveDateUtc: null);

        json.Should().Contain("\"refresh\"");
        json.Should().Contain("\"type\": \"full\"");
        json.Should().Contain("\"FactSales\"");
        json.Should().NotContain("applyRefreshPolicy");
    }

    [Fact]
    public void Builds_policy_refresh_with_apply_true_and_effective_date()
    {
        var json = new RefreshCommandBuilder().Build(
            databaseName: "TinyStatic",
            entries: new[]
            {
                new RefreshTableEntry("FactSales",
                    new RefreshTypeSpec(RefreshTypeSpec.RefreshKind.Policy, ApplyRefreshPolicy: true))
            },
            effectiveDateUtc: "2026-04-17");

        json.Should().Contain("\"type\": \"full\"");
        json.Should().Contain("\"applyRefreshPolicy\": true");
        json.Should().Contain("\"effectiveDate\": \"2026-04-17\"");
    }
}

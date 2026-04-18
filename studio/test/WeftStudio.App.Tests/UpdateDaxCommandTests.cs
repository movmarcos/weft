// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using WeftStudio.App.Commands;

namespace WeftStudio.App.Tests;

public class UpdateDaxCommandTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim");

    [Fact]
    public void Apply_updates_measure_expression()
    {
        var s = ModelSession.OpenBim(FixturePath);
        var measure = s.Database.Model.Tables["FactSales"].Measures[0];
        var original = measure.Expression;
        var cmd = new UpdateDaxCommand("FactSales", measure.Name, original, "SUM(FactSales[Amount])*2");

        cmd.Apply(s.Database);

        s.Database.Model.Tables["FactSales"].Measures[measure.Name].Expression
            .Should().Be("SUM(FactSales[Amount])*2");
    }

    [Fact]
    public void Revert_restores_original_expression()
    {
        var s = ModelSession.OpenBim(FixturePath);
        var measure = s.Database.Model.Tables["FactSales"].Measures[0];
        var original = measure.Expression;
        var cmd = new UpdateDaxCommand("FactSales", measure.Name, original, "SUM(FactSales[Amount])*2");

        cmd.Apply(s.Database);
        cmd.Revert(s.Database);

        s.Database.Model.Tables["FactSales"].Measures[measure.Name].Expression
            .Should().Be(original);
    }

    [Fact]
    public void Description_is_human_readable()
    {
        var cmd = new UpdateDaxCommand("FactSales", "Total Sales", "OLD", "NEW");
        cmd.Description.Should().Be("Update DAX for FactSales[Total Sales]");
    }
}

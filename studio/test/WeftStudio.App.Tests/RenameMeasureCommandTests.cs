// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using WeftStudio.App.Commands;

namespace WeftStudio.App.Tests;

public class RenameMeasureCommandTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim");

    [Fact]
    public void Apply_renames_measure_on_table()
    {
        var session = ModelSession.OpenBim(FixturePath);
        var cmd = new RenameMeasureCommand("FactSales", "Total Sales", "Revenue");

        cmd.Apply(session.Database);

        session.Database.Model.Tables["FactSales"]
            .Measures.Contains("Total Sales").Should().BeFalse();
        session.Database.Model.Tables["FactSales"]
            .Measures.Contains("Revenue").Should().BeTrue();
    }

    [Fact]
    public void Revert_restores_original_name()
    {
        var session = ModelSession.OpenBim(FixturePath);
        var cmd = new RenameMeasureCommand("FactSales", "Total Sales", "Revenue");

        cmd.Apply(session.Database);
        cmd.Revert(session.Database);

        session.Database.Model.Tables["FactSales"]
            .Measures.Contains("Total Sales").Should().BeTrue();
    }

    [Fact]
    public void Apply_throws_when_target_name_already_exists()
    {
        var session = ModelSession.OpenBim(FixturePath);
        var existing = session.Database.Model.Tables["FactSales"].Measures[0].Name;
        var otherExisting = session.Database.Model.Tables["FactSales"].Measures.Count > 1
            ? session.Database.Model.Tables["FactSales"].Measures[1].Name
            : existing;

        if (existing == otherExisting) return; // skip if fixture only has one measure

        var cmd = new RenameMeasureCommand("FactSales", existing, otherExisting);

        Action act = () => cmd.Apply(session.Database);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{otherExisting}*");
    }

    [Fact]
    public void Description_is_human_readable()
    {
        var cmd = new RenameMeasureCommand("FactSales", "Total Sales", "Revenue");
        cmd.Description.Should().Be("Rename measure FactSales[Total Sales] → Revenue");
    }
}

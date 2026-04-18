// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using WeftStudio.App.Commands;

namespace WeftStudio.App.Tests;

public class ChangeTrackerTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim");

    [Fact]
    public void Execute_applies_command_and_marks_dirty()
    {
        var s = ModelSession.OpenBim(FixturePath);
        var cmd = new RenameMeasureCommand("FactSales", "Total Sales", "Revenue");

        s.ChangeTracker.Execute(s.Database, cmd);

        s.IsDirty.Should().BeTrue();
        s.ChangeTracker.UndoHistory.Should().ContainSingle()
            .Which.Description.Should().Contain("Revenue");
    }

    [Fact]
    public void Undo_reverts_the_last_command()
    {
        var s = ModelSession.OpenBim(FixturePath);
        s.ChangeTracker.Execute(s.Database,
            new RenameMeasureCommand("FactSales", "Total Sales", "Revenue"));

        s.ChangeTracker.Undo(s.Database);

        s.Database.Model.Tables["FactSales"]
            .Measures.Contains("Total Sales").Should().BeTrue();
        s.ChangeTracker.UndoHistory.Should().BeEmpty();
        s.ChangeTracker.RedoHistory.Should().ContainSingle();
    }

    [Fact]
    public void Redo_reapplies_an_undone_command()
    {
        var s = ModelSession.OpenBim(FixturePath);
        s.ChangeTracker.Execute(s.Database,
            new RenameMeasureCommand("FactSales", "Total Sales", "Revenue"));
        s.ChangeTracker.Undo(s.Database);

        s.ChangeTracker.Redo(s.Database);

        s.Database.Model.Tables["FactSales"]
            .Measures.Contains("Revenue").Should().BeTrue();
    }

    [Fact]
    public void New_Execute_after_Undo_clears_redo_stack()
    {
        var s = ModelSession.OpenBim(FixturePath);
        s.ChangeTracker.Execute(s.Database,
            new RenameMeasureCommand("FactSales", "Total Sales", "Revenue"));
        s.ChangeTracker.Undo(s.Database);
        s.ChangeTracker.Execute(s.Database,
            new RenameMeasureCommand("FactSales", "Total Sales", "Revenue2"));

        s.ChangeTracker.RedoHistory.Should().BeEmpty();
    }

    [Fact]
    public void MarkClean_clears_dirty_and_preserves_history()
    {
        var s = ModelSession.OpenBim(FixturePath);
        s.ChangeTracker.Execute(s.Database,
            new RenameMeasureCommand("FactSales", "Total Sales", "Revenue"));

        s.ChangeTracker.MarkClean();

        s.IsDirty.Should().BeFalse();
        s.ChangeTracker.UndoHistory.Should().ContainSingle();
    }
}

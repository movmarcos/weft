// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using WeftStudio.App.Commands;
using WeftStudio.App.Persistence;

namespace WeftStudio.App.Tests;

public class BimSaverTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim");

    [Fact]
    public void Save_writes_current_database_state_to_disk()
    {
        var tmp = Path.GetTempFileName() + ".bim";
        File.Copy(FixturePath, tmp, overwrite: true);

        try
        {
            var s = ModelSession.OpenBim(tmp);
            s.ChangeTracker.Execute(s.Database,
                new RenameMeasureCommand("FactSales", "Total Sales", "Revenue"));
            BimSaver.Save(s);

            var reloaded = ModelSession.OpenBim(tmp);
            reloaded.Database.Model.Tables["FactSales"].Measures.Contains("Revenue")
                .Should().BeTrue();
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public void Save_marks_session_clean()
    {
        var tmp = Path.GetTempFileName() + ".bim";
        File.Copy(FixturePath, tmp, overwrite: true);

        try
        {
            var s = ModelSession.OpenBim(tmp);
            s.ChangeTracker.Execute(s.Database,
                new RenameMeasureCommand("FactSales", "Total Sales", "Revenue"));
            s.IsDirty.Should().BeTrue();

            BimSaver.Save(s);

            s.IsDirty.Should().BeFalse();
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public void Save_throws_when_session_has_no_source_path()
    {
        var s = ModelSession.OpenBim(FixturePath);
        var sessionWithoutPath = new ModelSession(s.Database, sourcePath: null);

        Action act = () => BimSaver.Save(sessionWithoutPath);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*no source path*");
    }
}

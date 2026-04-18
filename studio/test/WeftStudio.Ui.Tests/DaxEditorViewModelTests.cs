// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using WeftStudio.App;
using WeftStudio.Ui.DaxEditor;

namespace WeftStudio.Ui.Tests;

public class DaxEditorViewModelTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim");

    [Fact]
    public void Commit_applies_UpdateDaxCommand_to_session()
    {
        var s = ModelSession.OpenBim(FixturePath);
        var m = s.Database.Model.Tables["FactSales"].Measures[0];
        var vm = new DaxEditorViewModel(s, "FactSales", m.Name);

        vm.Text = "SUM(FactSales[Amount])*2";
        vm.Commit();

        s.Database.Model.Tables["FactSales"].Measures[m.Name].Expression
            .Should().Be("SUM(FactSales[Amount])*2");
        s.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void Commit_no_op_when_text_unchanged()
    {
        var s = ModelSession.OpenBim(FixturePath);
        var m = s.Database.Model.Tables["FactSales"].Measures[0];
        var vm = new DaxEditorViewModel(s, "FactSales", m.Name);

        vm.Commit();

        s.IsDirty.Should().BeFalse();
    }
}

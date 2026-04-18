// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Reactive.Linq;
using FluentAssertions;
using WeftStudio.App;
using WeftStudio.Ui.Shell;

namespace WeftStudio.Ui.Tests;

public class EndToEndSmokeTests
{
    [Fact]
    public async System.Threading.Tasks.Task Open_edit_save_reload_round_trip()
    {
        var tmp = Path.GetTempFileName() + ".bim";
        File.Copy(Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim"),
                  tmp, overwrite: true);
        try
        {
            var vm = new ShellViewModel();
            vm.OpenModel(tmp);
            var measure = vm.Explorer!.Session.Database.Model.Tables["FactSales"].Measures[0];

            vm.OpenMeasure("FactSales", measure.Name);
            vm.ActiveTab!.Text = "SUM(FactSales[Amount]) * 2";
            vm.ActiveTab.Commit();

            await vm.SaveCommand.Execute().FirstAsync();

            var vm2 = new ShellViewModel();
            vm2.OpenModel(tmp);
            vm2.Explorer!.Session.Database.Model.Tables["FactSales"]
                .Measures[measure.Name].Expression.Should().Be("SUM(FactSales[Amount]) * 2");
        }
        finally { File.Delete(tmp); }
    }
}

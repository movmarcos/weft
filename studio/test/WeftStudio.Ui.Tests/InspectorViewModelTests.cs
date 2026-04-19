// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using WeftStudio.App;
using WeftStudio.Ui.Inspector;

namespace WeftStudio.Ui.Tests;

public class InspectorViewModelTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim");

    [Fact]
    public void Reflects_measure_object_type_and_name()
    {
        var s = ModelSession.OpenBim(FixturePath);
        var m = s.Database.Model.Tables["FactSales"].Measures[0];

        var vm = new InspectorViewModel(m);

        vm.ObjectType.Should().Be("MEASURE");
        vm.ObjectName.Should().Be(m.Name);
    }

    [Fact]
    public void Populates_scalar_properties_for_measure()
    {
        var s = ModelSession.OpenBim(FixturePath);
        var m = s.Database.Model.Tables["FactSales"].Measures[0];

        var vm = new InspectorViewModel(m);

        // Measure has scalar properties like Name, Expression, FormatString, IsHidden, etc.
        vm.Properties.Should().NotBeEmpty();
        vm.Properties.Select(p => p.Name).Should().Contain("Name");
        vm.Properties.Select(p => p.Name).Should().Contain("Expression");
    }

    [Fact]
    public void Reflects_table_object_type_and_name()
    {
        var s = ModelSession.OpenBim(FixturePath);
        var t = s.Database.Model.Tables["FactSales"];

        var vm = new InspectorViewModel(t);

        vm.ObjectType.Should().Be("TABLE");
        vm.ObjectName.Should().Be("FactSales");
        vm.Properties.Should().NotBeEmpty();
    }

    [Fact]
    public void Skips_collection_and_complex_properties()
    {
        var s = ModelSession.OpenBim(FixturePath);
        var t = s.Database.Model.Tables["FactSales"];

        var vm = new InspectorViewModel(t);

        // Collections like Columns, Measures, Partitions should NOT appear as scalar rows.
        vm.Properties.Select(p => p.Name).Should().NotContain("Columns");
        vm.Properties.Select(p => p.Name).Should().NotContain("Measures");
        vm.Properties.Select(p => p.Name).Should().NotContain("Partitions");
    }
}

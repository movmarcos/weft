// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Diffing;
using Weft.Xmla;

namespace Weft.Xmla.Tests;

public class RefreshTypeSelectorTests
{
    private static Table NewTable(string name) => new() { Name = name };

    private static TablePlan AddPlan(string name, TableClassification c) =>
        new(name, c, NewTable(name));

    private static TableDiff AlterDiff(string name, TableClassification c, bool policyChanged = false) =>
        new(name, c, policyChanged,
            ColumnsAdded: Array.Empty<string>(),
            ColumnsRemoved: Array.Empty<string>(),
            ColumnsModified: Array.Empty<string>(),
            MeasuresAdded: Array.Empty<string>(),
            MeasuresRemoved: Array.Empty<string>(),
            MeasuresModified: Array.Empty<string>(),
            HierarchiesChanged: Array.Empty<string>(),
            PartitionStrategy: PartitionStrategy.PreserveTarget,
            SourceTable: NewTable(name),
            TargetTable: NewTable(name));

    [Fact]
    public void Static_added_table_uses_Full_refresh_no_policy_application()
    {
        var sel = new RefreshTypeSelector();
        var spec = sel.For(AddPlan("T", TableClassification.Static));
        spec.RefreshType.Should().Be(RefreshTypeSpec.RefreshKind.Full);
        spec.ApplyRefreshPolicy.Should().BeFalse();
    }

    [Fact]
    public void Incremental_added_table_uses_Policy_with_apply_true()
    {
        var sel = new RefreshTypeSelector();
        var spec = sel.For(AddPlan("T", TableClassification.IncrementalRefreshPolicy));
        spec.RefreshType.Should().Be(RefreshTypeSpec.RefreshKind.Policy);
        spec.ApplyRefreshPolicy.Should().BeTrue();
    }

    [Fact]
    public void Incremental_altered_with_policy_change_uses_Policy_with_apply_true()
    {
        var sel = new RefreshTypeSelector();
        var spec = sel.For(AlterDiff("T", TableClassification.IncrementalRefreshPolicy, policyChanged: true));
        spec.RefreshType.Should().Be(RefreshTypeSpec.RefreshKind.Policy);
        spec.ApplyRefreshPolicy.Should().BeTrue();
    }

    [Fact]
    public void Incremental_altered_with_only_schema_change_uses_Policy_with_apply_false()
    {
        var sel = new RefreshTypeSelector();
        var spec = sel.For(AlterDiff("T", TableClassification.IncrementalRefreshPolicy, policyChanged: false));
        spec.RefreshType.Should().Be(RefreshTypeSpec.RefreshKind.Policy);
        spec.ApplyRefreshPolicy.Should().BeFalse();
    }

    [Fact]
    public void Dynamic_altered_uses_Full_no_policy()
    {
        var sel = new RefreshTypeSelector();
        var spec = sel.For(AlterDiff("T", TableClassification.DynamicallyPartitioned));
        spec.RefreshType.Should().Be(RefreshTypeSpec.RefreshKind.Full);
        spec.ApplyRefreshPolicy.Should().BeFalse();
    }
}

// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Diffing;
using Weft.Core.RefreshPolicy;
using Xunit;

namespace Weft.Core.Tests.RefreshPolicy;

public class HistoryLossGateTests
{
    private static BasicRefreshPolicy P(int years) => new()
    {
        RollingWindowGranularity = RefreshGranularityType.Year,
        RollingWindowPeriods = years,
        IncrementalGranularity = RefreshGranularityType.Day,
        IncrementalPeriods = 10,
        SourceExpression = "let s = ... in s"
    };

    [Fact]
    public void No_violation_when_no_policy_changes()
    {
        var target = new Database { Name = "D", CompatibilityLevel = 1600 };
        target.Model = new Model();
        var gate = new HistoryLossGate(new RetentionCalculator(new DateOnly(2026, 4, 17)));

        gate.Check(
            changeSet: EmptyChangeSet(),
            target: target,
            allowHistoryLoss: false).Should().BeEmpty();
    }

    [Fact]
    public void Violation_when_policy_shrinks_and_not_allowed()
    {
        var target = Fixture(P(5), new[] { "Year2021", "Year2022", "Year2023", "Year2024", "Year2025" });
        var srcTableWithShorterPolicy = new Table { Name = "FactSales", RefreshPolicy = P(3) };
        var diff = new TableDiff(
            Name: "FactSales",
            Classification: TableClassification.IncrementalRefreshPolicy,
            RefreshPolicyChanged: true,
            ColumnsAdded: Array.Empty<string>(), ColumnsRemoved: Array.Empty<string>(),
            ColumnsModified: Array.Empty<string>(),
            MeasuresAdded: Array.Empty<string>(), MeasuresRemoved: Array.Empty<string>(),
            MeasuresModified: Array.Empty<string>(),
            HierarchiesChanged: Array.Empty<string>(),
            PartitionStrategy: PartitionStrategy.PreserveTarget,
            SourceTable: srcTableWithShorterPolicy,
            TargetTable: target.Model.Tables["FactSales"]);

        var cs = new ChangeSet(
            Array.Empty<TablePlan>(), Array.Empty<string>(), new[] { diff },
            Array.Empty<string>(),
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<string>());

        var gate = new HistoryLossGate(new RetentionCalculator(new DateOnly(2026, 4, 17)));
        var violations = gate.Check(cs, target, allowHistoryLoss: false);

        violations.Should().ContainSingle()
            .Which.LostPartitions.Should().Contain("Year2021");
    }

    [Fact]
    public void Returns_empty_when_allowHistoryLoss_true_even_on_shrink()
    {
        var target = Fixture(P(5), new[] { "Year2021", "Year2022", "Year2023", "Year2024", "Year2025" });
        var diff = new TableDiff(
            "FactSales", TableClassification.IncrementalRefreshPolicy, true,
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<string>(), PartitionStrategy.PreserveTarget,
            new Table { Name = "FactSales", RefreshPolicy = P(3) },
            target.Model.Tables["FactSales"]);

        var cs = new ChangeSet(
            Array.Empty<TablePlan>(), Array.Empty<string>(), new[] { diff },
            Array.Empty<string>(),
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<string>());

        var gate = new HistoryLossGate(new RetentionCalculator(new DateOnly(2026, 4, 17)));
        gate.Check(cs, target, allowHistoryLoss: true).Should().BeEmpty();
    }

    private static ChangeSet EmptyChangeSet() => new(
        Array.Empty<TablePlan>(), Array.Empty<string>(),
        Array.Empty<TableDiff>(), Array.Empty<string>(),
        Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
        Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
        Array.Empty<string>());

    private static Database Fixture(BasicRefreshPolicy policy, string[] partitions)
    {
        var db = new Database { Name = "D", CompatibilityLevel = 1600 };
        db.Model = new Model();
        var t = new Table { Name = "FactSales", RefreshPolicy = policy };
        foreach (var p in partitions)
        {
            t.Partitions.Add(new Partition
            {
                Name = p,
                Mode = ModeType.Import,
                Source = new MPartitionSource { Expression = "x" }
            });
        }
        db.Model.Tables.Add(t);
        return db;
    }
}

// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Diffing;

namespace Weft.Core.RefreshPolicy;

public sealed record HistoryLossViolation(
    string TableName,
    IReadOnlyList<string> LostPartitions);

public sealed class HistoryLossGate
{
    private readonly RetentionCalculator _calc;

    public HistoryLossGate(RetentionCalculator calc)
    {
        _calc = calc;
    }

    public IReadOnlyList<HistoryLossViolation> Check(
        ChangeSet changeSet, Database target, bool allowHistoryLoss)
    {
        if (allowHistoryLoss) return Array.Empty<HistoryLossViolation>();

        var violations = new List<HistoryLossViolation>();
        foreach (var alter in changeSet.TablesToAlter)
        {
            if (alter.Classification != TableClassification.IncrementalRefreshPolicy) continue;
            if (!alter.RefreshPolicyChanged) continue;

            var oldPolicy = alter.TargetTable.RefreshPolicy as BasicRefreshPolicy;
            var newPolicy = alter.SourceTable.RefreshPolicy as BasicRefreshPolicy;
            if (oldPolicy is null || newPolicy is null) continue;

            if (!target.Model.Tables.ContainsName(alter.Name)) continue;
            var existing = target.Model.Tables[alter.Name].Partitions.Select(p => p.Name).ToList();

            var lost = _calc.PartitionsRemovedBy(oldPolicy, newPolicy, existing);
            if (lost.Count > 0)
                violations.Add(new HistoryLossViolation(alter.Name, lost));
        }
        return violations;
    }
}

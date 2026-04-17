// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Diffing.Comparers;
using Weft.Core.RefreshPolicy;

namespace Weft.Core.Diffing;

public sealed class ModelDiffer
{
    private readonly TableClassifier _classifier = new();
    private readonly ColumnComparer _columns = new();
    private readonly MeasureComparer _measures = new();
    private readonly RefreshPolicyComparer _policies = new();

    public ChangeSet Compute(Database source, Database target)
    {
        var srcTables = source.Model.Tables.ToDictionary(t => t.Name, StringComparer.Ordinal);
        var tgtTables = target.Model.Tables.ToDictionary(t => t.Name, StringComparer.Ordinal);

        var add = srcTables.Keys.Except(tgtTables.Keys).Select(n => MakeAdd(srcTables[n])).ToList();
        var drop = tgtTables.Keys.Except(srcTables.Keys).OrderBy(x => x).ToList();

        var alter = new List<TableDiff>();
        var unchanged = new List<string>();
        foreach (var name in srcTables.Keys.Intersect(tgtTables.Keys))
        {
            var diff = DiffTable(srcTables[name], tgtTables[name]);
            if (diff is null) unchanged.Add(name);
            else alter.Add(diff);
        }

        return new ChangeSet(
            TablesToAdd: add,
            TablesToDrop: drop,
            TablesToAlter: alter,
            TablesUnchanged: unchanged,
            MeasuresChanged: Array.Empty<string>(),
            RelationshipsChanged: Array.Empty<string>(),
            RolesChanged: Array.Empty<string>(),
            PerspectivesChanged: Array.Empty<string>(),
            CulturesChanged: Array.Empty<string>(),
            ExpressionsChanged: Array.Empty<string>(),
            DataSourcesChanged: Array.Empty<string>());
    }

    private TablePlan MakeAdd(Table sourceTable) =>
        new(sourceTable.Name, _classifier.Classify(sourceTable, null), sourceTable);

    private TableDiff? DiffTable(Table src, Table tgt)
    {
        var classification = _classifier.Classify(src, tgt);
        var policyChanged = !_policies.AreEqual(src.RefreshPolicy, tgt.RefreshPolicy);
        var cols = _columns.Compare(src.Columns, tgt.Columns);
        var meas = _measures.Compare(src.Measures, tgt.Measures);

        var hasChange =
            policyChanged
            || cols.Added.Count + cols.Removed.Count + cols.Modified.Count > 0
            || meas.Added.Count + meas.Removed.Count + meas.Modified.Count > 0;

        if (!hasChange) return null;

        return new TableDiff(
            Name: src.Name,
            Classification: classification,
            RefreshPolicyChanged: policyChanged,
            ColumnsAdded: cols.Added, ColumnsRemoved: cols.Removed, ColumnsModified: cols.Modified,
            MeasuresAdded: meas.Added, MeasuresRemoved: meas.Removed, MeasuresModified: meas.Modified,
            HierarchiesChanged: Array.Empty<string>(),
            PartitionStrategy: PartitionStrategy.PreserveTarget,
            SourceTable: src,
            TargetTable: tgt);
    }
}

// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace Weft.Core.Diffing;

public sealed record ChangeSet(
    IReadOnlyList<TablePlan> TablesToAdd,
    IReadOnlyList<string> TablesToDrop,
    IReadOnlyList<TableDiff> TablesToAlter,
    IReadOnlyList<string> TablesUnchanged,
    IReadOnlyList<string> MeasuresChanged,
    IReadOnlyList<string> RelationshipsChanged,
    IReadOnlyList<string> RolesChanged,
    IReadOnlyList<string> PerspectivesChanged,
    IReadOnlyList<string> CulturesChanged,
    IReadOnlyList<string> ExpressionsChanged,
    IReadOnlyList<string> DataSourcesChanged)
{
    public bool IsEmpty =>
        TablesToAdd.Count == 0 && TablesToDrop.Count == 0 && TablesToAlter.Count == 0
        && MeasuresChanged.Count == 0 && RelationshipsChanged.Count == 0
        && RolesChanged.Count == 0 && PerspectivesChanged.Count == 0
        && CulturesChanged.Count == 0 && ExpressionsChanged.Count == 0
        && DataSourcesChanged.Count == 0;

    public IEnumerable<string> RefreshTargets =>
        TablesToAdd.Select(t => t.Name).Concat(TablesToAlter.Select(t => t.Name));
}

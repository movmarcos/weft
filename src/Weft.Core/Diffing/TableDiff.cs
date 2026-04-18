// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AnalysisServices.Tabular;

namespace Weft.Core.Diffing;

public enum PartitionStrategy { PreserveTarget, UseSource }

public sealed record TableDiff(
    string Name,
    TableClassification Classification,
    bool RefreshPolicyChanged,
    IReadOnlyList<string> ColumnsAdded,
    IReadOnlyList<string> ColumnsRemoved,
    IReadOnlyList<string> ColumnsModified,
    IReadOnlyList<string> MeasuresAdded,
    IReadOnlyList<string> MeasuresRemoved,
    IReadOnlyList<string> MeasuresModified,
    IReadOnlyList<string> HierarchiesChanged,
    PartitionStrategy PartitionStrategy,
    Table SourceTable,
    Table TargetTable);

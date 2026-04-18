// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AnalysisServices.Tabular;

namespace Weft.Core.Diffing;

public sealed class TableClassifier
{
    public TableClassification Classify(Table? source, Table? target)
    {
        var sourceHasPolicy = source?.RefreshPolicy is not null;
        var targetHasPolicy = target?.RefreshPolicy is not null;
        if (sourceHasPolicy || targetHasPolicy)
            return TableClassification.IncrementalRefreshPolicy;

        if (source is null || target is null)
            return TableClassification.Static;

        var sourceNames = source.Partitions.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        if (target.Partitions.Any(p => !sourceNames.Contains(p.Name)))
            return TableClassification.DynamicallyPartitioned;

        return TableClassification.Static;
    }
}

// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Weft.Core.Diffing;

namespace Weft.Xmla;

public sealed record RefreshTypeSpec(
    RefreshTypeSpec.RefreshKind RefreshType,
    bool ApplyRefreshPolicy)
{
    public enum RefreshKind { Full, Policy, DataOnly, Calculate, Automatic }
}

public sealed class RefreshTypeSelector
{
    public RefreshTypeSpec For(TablePlan add) => add.Classification switch
    {
        TableClassification.IncrementalRefreshPolicy =>
            new(RefreshTypeSpec.RefreshKind.Policy, ApplyRefreshPolicy: true),
        TableClassification.DynamicallyPartitioned =>
            new(RefreshTypeSpec.RefreshKind.Full, ApplyRefreshPolicy: false),
        TableClassification.Static =>
            new(RefreshTypeSpec.RefreshKind.Full, ApplyRefreshPolicy: false),
        _ => throw new ArgumentOutOfRangeException(nameof(add), add.Classification, "Unknown classification.")
    };

    public RefreshTypeSpec For(TableDiff alter) => alter.Classification switch
    {
        TableClassification.IncrementalRefreshPolicy =>
            new(RefreshTypeSpec.RefreshKind.Policy, ApplyRefreshPolicy: alter.RefreshPolicyChanged),
        TableClassification.DynamicallyPartitioned =>
            new(RefreshTypeSpec.RefreshKind.Full, ApplyRefreshPolicy: false),
        TableClassification.Static =>
            new(RefreshTypeSpec.RefreshKind.Full, ApplyRefreshPolicy: false),
        _ => throw new ArgumentOutOfRangeException(nameof(alter), alter.Classification, "Unknown classification.")
    };
}

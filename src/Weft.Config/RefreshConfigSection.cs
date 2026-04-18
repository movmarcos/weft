// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace Weft.Config;

public sealed record RefreshConfigSection(
    string? Type,
    int? MaxParallelism,
    int? PollIntervalSeconds,
    IncrementalPolicyConfig? IncrementalPolicy,
    DynamicPartitionStrategyConfig? DynamicPartitionStrategy);

public sealed record IncrementalPolicyConfig(
    bool ApplyOnFirstDeploy,
    bool ApplyOnPolicyChange,
    string BookmarkMode);

public sealed record DynamicPartitionStrategyConfig(
    string Mode,
    int NewestN);

// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace Weft.Core.Partitions;

public sealed record PartitionManifest(
    DateTimeOffset CapturedAtUtc,
    string TargetDatabase,
    IReadOnlyDictionary<string, IReadOnlyList<PartitionRecord>> Tables);

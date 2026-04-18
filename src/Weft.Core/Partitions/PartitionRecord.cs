// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace Weft.Core.Partitions;

public sealed record PartitionRecord(
    string Name,
    string? RefreshBookmark,
    DateTime? ModifiedTime,
    long? RowCount);

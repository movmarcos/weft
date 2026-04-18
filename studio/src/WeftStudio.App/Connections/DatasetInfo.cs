// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace WeftStudio.App.Connections;

/// <summary>
/// One row in the dataset picker grid.
/// Fields may be null/0 when XMLA doesn't surface them — the grid shows "-" in that case.
/// </summary>
public sealed record DatasetInfo(
    string Name,
    long? SizeBytes,
    DateTime? LastUpdatedUtc,
    string? RefreshPolicy,
    string? Owner);

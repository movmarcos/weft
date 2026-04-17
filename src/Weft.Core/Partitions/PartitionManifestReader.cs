// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AnalysisServices.Tabular;

namespace Weft.Core.Partitions;

public sealed class PartitionManifestReader
{
    public PartitionManifest Read(Database database)
    {
        var tables = new Dictionary<string, IReadOnlyList<PartitionRecord>>();
        foreach (var table in database.Model.Tables)
        {
            var records = table.Partitions
                .Select(p => new PartitionRecord(
                    Name: p.Name,
                    RefreshBookmark: p.Annotations.Find(PartitionAnnotationNames.RefreshBookmark)?.Value is { Length: > 0 } v ? v : null,
                    ModifiedTime: p.ModifiedTime == default ? null : p.ModifiedTime,
                    RowCount: null))
                .ToList();
            tables[table.Name] = records;
        }

        return new PartitionManifest(
            CapturedAtUtc: DateTimeOffset.UtcNow,
            TargetDatabase: database.Name,
            Tables: tables);
    }
}

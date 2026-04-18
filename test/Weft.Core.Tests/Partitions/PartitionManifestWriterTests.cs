// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using FluentAssertions;
using Weft.Core.Partitions;
using Xunit;

namespace Weft.Core.Tests.Partitions;

public class PartitionManifestWriterTests
{
    [Fact]
    public void Round_trips_manifest_through_json()
    {
        var manifest = new PartitionManifest(
            CapturedAtUtc: new DateTimeOffset(2026, 4, 17, 10, 0, 0, TimeSpan.Zero),
            TargetDatabase: "TinyStatic",
            Tables: new Dictionary<string, IReadOnlyList<PartitionRecord>>
            {
                ["FactSales"] = new[] { new PartitionRecord("FactSales", "wm-001", null, null) }
            });

        var json = new PartitionManifestWriter().ToJson(manifest);
        var parsed = JsonSerializer.Deserialize<PartitionManifest>(json,
            PartitionManifestWriter.JsonOptions)!;

        parsed.TargetDatabase.Should().Be("TinyStatic");
        parsed.Tables["FactSales"][0].RefreshBookmark.Should().Be("wm-001");
    }
}

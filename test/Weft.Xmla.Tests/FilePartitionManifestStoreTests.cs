// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Weft.Core.Partitions;
using Weft.Xmla;

namespace Weft.Xmla.Tests;

public class FilePartitionManifestStoreTests
{
    [Fact]
    public void Writes_manifest_to_artifacts_dir_and_returns_path()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var manifest = new PartitionManifest(
                CapturedAtUtc: DateTimeOffset.UtcNow,
                TargetDatabase: "TinyStatic",
                Tables: new Dictionary<string, IReadOnlyList<PartitionRecord>>
                {
                    ["FactSales"] = new[] { new PartitionRecord("FactSales", "wm-001", null, null) }
                });

            var store = new FilePartitionManifestStore();
            var path = store.Write(manifest, dir, "pre-partitions");

            File.Exists(path).Should().BeTrue();
            Path.GetFileName(path).Should().Contain("pre-partitions");
            Path.GetFileName(path).Should().EndWith(".json");

            var reread = store.Read(path);
            reread.TargetDatabase.Should().Be("TinyStatic");
            reread.Tables["FactSales"][0].RefreshBookmark.Should().Be("wm-001");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}

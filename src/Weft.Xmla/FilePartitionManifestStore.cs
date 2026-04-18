// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using Weft.Core.Abstractions;
using Weft.Core.Partitions;

namespace Weft.Xmla;

public sealed class FilePartitionManifestStore : IPartitionManifestStore
{
    private readonly PartitionManifestWriter _writer = new();

    public string Write(PartitionManifest manifest, string artifactsDirectory, string label)
    {
        Directory.CreateDirectory(artifactsDirectory);
        var ts = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var safeLabel = string.Concat(label.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));
        var fileName = $"{ts}-{manifest.TargetDatabase}-{safeLabel}.json";
        var path = Path.Combine(artifactsDirectory, fileName);
        _writer.Write(manifest, path);
        return path;
    }

    public PartitionManifest Read(string path)
        => JsonSerializer.Deserialize<PartitionManifest>(
            File.ReadAllText(path),
            PartitionManifestWriter.JsonOptions)!;
}

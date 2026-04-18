// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Weft.Core.Partitions;

public sealed class PartitionManifestWriter
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ToJson(PartitionManifest manifest)
        => JsonSerializer.Serialize(manifest, JsonOptions);

    public void Write(PartitionManifest manifest, string path)
        => File.WriteAllText(path, ToJson(manifest));
}

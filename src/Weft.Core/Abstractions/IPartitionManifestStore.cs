// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Weft.Core.Partitions;

namespace Weft.Core.Abstractions;

public interface IPartitionManifestStore
{
    string Write(PartitionManifest manifest, string artifactsDirectory, string label);
    PartitionManifest Read(string path);
}

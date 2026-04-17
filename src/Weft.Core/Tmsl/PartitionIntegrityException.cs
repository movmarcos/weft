// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace Weft.Core.Tmsl;

public sealed class PartitionIntegrityException : Exception
{
    public PartitionIntegrityException(string message) : base(message) { }
}

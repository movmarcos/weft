// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace Weft.Config;

public sealed class WeftConfigValidationException : Exception
{
    public WeftConfigValidationException(string message) : base(message) {}
}

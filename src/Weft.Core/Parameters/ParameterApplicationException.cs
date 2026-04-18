// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace Weft.Core.Parameters;

public sealed class ParameterApplicationException : Exception
{
    public ParameterApplicationException(string message) : base(message) {}
}

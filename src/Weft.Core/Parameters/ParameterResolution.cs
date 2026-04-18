// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace Weft.Core.Parameters;

public sealed record ParameterResolution(
    string Name,
    string DeclaredType,
    object? RawValue,
    ParameterValueSource Source);

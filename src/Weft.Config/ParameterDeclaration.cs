// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace Weft.Config;

public sealed record ParameterDeclaration(
    string Name,
    string? Description,
    string Type,
    bool Required,
    object? Default);

// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace Weft.Core.Abstractions;

public sealed record XmlaExecutionResult(
    bool Success,
    IReadOnlyList<string> Messages,
    TimeSpan Duration);

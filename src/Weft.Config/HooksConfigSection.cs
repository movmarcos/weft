// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace Weft.Config;

public sealed record HooksConfigSection(
    string? PrePlan,
    string? PreDeploy,
    string? PostDeploy,
    string? PreRefresh,
    string? PostRefresh,
    string? OnFailure);

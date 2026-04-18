// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace Weft.Config;

public sealed record ProfileConfig(
    string Workspace,
    string Database,
    AuthConfigSection Auth,
    RefreshConfigSection? Refresh,
    bool? AllowDrops,
    bool? AllowHistoryLoss,
    IReadOnlyDictionary<string, object?> Parameters);

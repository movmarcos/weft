// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Weft.Core.Diffing;

namespace Weft.Core.Abstractions;

public sealed record RefreshRequest(
    string WorkspaceUrl,
    string DatabaseName,
    AccessToken Token,
    ChangeSet ChangeSet,
    string? EffectiveDateUtc = null);

public interface IRefreshRunner
{
    Task<XmlaExecutionResult> RefreshAsync(
        RefreshRequest request,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}

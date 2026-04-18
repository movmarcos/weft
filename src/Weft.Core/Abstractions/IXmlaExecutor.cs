// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace Weft.Core.Abstractions;

public interface IXmlaExecutor
{
    Task<XmlaExecutionResult> ExecuteAsync(
        string workspaceUrl,
        string databaseName,
        AccessToken token,
        string tmslJson,
        CancellationToken cancellationToken = default);
}

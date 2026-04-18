// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AnalysisServices.Tabular;

namespace Weft.Core.Abstractions;

public interface ITargetReader
{
    Task<Database> ReadAsync(
        string workspaceUrl,
        string databaseName,
        AccessToken token,
        CancellationToken cancellationToken = default);
}

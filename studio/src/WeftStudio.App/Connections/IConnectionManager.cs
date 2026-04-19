// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Weft.Auth;
using Weft.Core.Abstractions;

namespace WeftStudio.App.Connections;

public interface IConnectionManager
{
    Task<AccessToken> SignInAsync(AuthOptions opts, CancellationToken ct);
    Task<IReadOnlyList<DatasetInfo>> ListDatasetsAsync(
        WorkspaceReference workspace, AccessToken token, CancellationToken ct);
    Task<ModelSession> FetchModelAsync(
        WorkspaceReference workspace, DatasetInfo dataset, AccessToken token, CancellationToken ct);
}

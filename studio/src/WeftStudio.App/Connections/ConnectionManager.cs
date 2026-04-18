// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Weft.Auth;
using Weft.Core.Abstractions;

namespace WeftStudio.App.Connections;

/// <summary>
/// Orchestrates the three async steps of opening a workspace-hosted model:
/// sign in, list datasets, fetch model. Stateless — callers hold onto
/// intermediate values (token, workspace ref) between calls.
/// </summary>
public sealed class ConnectionManager
{
    private readonly Func<AuthOptions, IAuthProvider> _authProviderFactory;
    private readonly ITargetReader _reader;

    public ConnectionManager(
        Func<AuthOptions, IAuthProvider> authProviderFactory,
        ITargetReader reader)
    {
        _authProviderFactory = authProviderFactory;
        _reader = reader;
    }

    public async Task<AccessToken> SignInAsync(AuthOptions opts, CancellationToken ct)
    {
        var provider = _authProviderFactory(opts);
        return await provider.GetTokenAsync(ct);
    }

    // Task 8 adds ListDatasetsAsync, Task 9 adds FetchModelAsync.
}

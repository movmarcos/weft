// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace Weft.Core.Abstractions;

public interface IAuthProvider
{
    Task<AccessToken> GetTokenAsync(CancellationToken cancellationToken = default);
}

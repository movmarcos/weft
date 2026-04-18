// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Weft.Core.Abstractions;

namespace Weft.Cli.Auth;

public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}

internal sealed class SystemClock : ISystemClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class TokenManager : IAuthProvider
{
    private readonly IAuthProvider _inner;
    private readonly TimeSpan _refreshAfter;
    private readonly ISystemClock _clock;
    private AccessToken? _cached;
    private DateTimeOffset _acquiredAt;

    public TokenManager(IAuthProvider inner, TimeSpan refreshAfter, ISystemClock? clock = null)
    {
        _inner = inner;
        _refreshAfter = refreshAfter;
        _clock = clock ?? new SystemClock();
    }

    public async Task<AccessToken> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null && _clock.UtcNow - _acquiredAt < _refreshAfter)
            return _cached;

        _cached = await _inner.GetTokenAsync(cancellationToken);
        _acquiredAt = _clock.UtcNow;
        return _cached;
    }
}

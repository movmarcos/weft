// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using NSubstitute;
using Weft.Cli.Auth;
using Weft.Core.Abstractions;
using Xunit;

namespace Weft.Cli.Tests;

public class TokenManagerTests
{
    [Fact]
    public async Task First_call_acquires_and_subsequent_reuse_cached()
    {
        var inner = Substitute.For<IAuthProvider>();
        inner.GetTokenAsync(default).ReturnsForAnyArgs(
            _ => Task.FromResult(new AccessToken("t1", DateTimeOffset.UtcNow.AddHours(1))));

        var mgr = new TokenManager(inner, TimeSpan.FromMinutes(30));

        var t1 = await mgr.GetTokenAsync();
        var t2 = await mgr.GetTokenAsync();

        t1.Value.Should().Be("t1");
        t2.Value.Should().Be("t1");
        await inner.ReceivedWithAnyArgs(1).GetTokenAsync(default);
    }

    [Fact]
    public async Task Refreshes_when_elapsed_time_exceeds_threshold()
    {
        var inner = Substitute.For<IAuthProvider>();
        var calls = 0;
        inner.GetTokenAsync(default).ReturnsForAnyArgs(_ =>
        {
            calls++;
            return Task.FromResult(new AccessToken("t" + calls, DateTimeOffset.UtcNow.AddHours(1)));
        });

        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var mgr = new TokenManager(inner, TimeSpan.FromMinutes(30), clock);

        (await mgr.GetTokenAsync()).Value.Should().Be("t1");
        clock.Advance(TimeSpan.FromMinutes(10));
        (await mgr.GetTokenAsync()).Value.Should().Be("t1");
        clock.Advance(TimeSpan.FromMinutes(25));
        (await mgr.GetTokenAsync()).Value.Should().Be("t2");
    }

    private sealed class FakeClock : ISystemClock
    {
        private DateTimeOffset _now;
        public FakeClock(DateTimeOffset start) { _now = start; }
        public DateTimeOffset UtcNow => _now;
        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}

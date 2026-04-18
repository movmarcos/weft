// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using NSubstitute;
using Weft.Auth;
using Weft.Core.Abstractions;
using WeftStudio.App.Connections;

namespace WeftStudio.App.Tests;

public class ConnectionManagerTests
{
    [Fact]
    public async Task SignInAsync_delegates_to_IAuthProvider_and_returns_AccessToken()
    {
        var auth = Substitute.For<IAuthProvider>();
        var fake = new AccessToken("jwt-token-value", DateTimeOffset.UtcNow.AddHours(1));
        auth.GetTokenAsync(Arg.Any<CancellationToken>()).Returns(fake);

        var factory = Substitute.For<Func<AuthOptions, IAuthProvider>>();
        factory(Arg.Any<AuthOptions>()).Returns(auth);

        var mgr = new ConnectionManager(factory, reader: Substitute.For<ITargetReader>());

        var opts = new AuthOptions(
            Mode: AuthMode.Interactive,
            TenantId: "",
            ClientId: "client-id");

        var token = await mgr.SignInAsync(opts, CancellationToken.None);

        token.Value.Should().Be("jwt-token-value");
    }
}

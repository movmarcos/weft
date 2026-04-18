// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Weft.Auth;

namespace Weft.Auth.Tests;

public class AuthProviderFactoryTests
{
    [Fact]
    public void Returns_secret_provider_for_secret_mode()
    {
        var opts = new AuthOptions(AuthMode.ServicePrincipalSecret, "tid", "cid", ClientSecret: "s");
        AuthProviderFactory.Create(opts).Should().BeOfType<ServicePrincipalSecretAuth>();
    }

    [Fact]
    public void Returns_interactive_provider_for_interactive_mode()
    {
        var opts = new AuthOptions(AuthMode.Interactive, "tid", "cid");
        AuthProviderFactory.Create(opts).Should().BeOfType<InteractiveAuth>();
    }

    [Fact]
    public void Returns_device_code_provider_for_device_mode()
    {
        var opts = new AuthOptions(AuthMode.DeviceCode, "tid", "cid");
        AuthProviderFactory.Create(opts).Should().BeOfType<DeviceCodeAuth>();
    }

    [Fact]
    public void Validates_options_before_constructing_provider()
    {
        var opts = new AuthOptions(AuthMode.ServicePrincipalSecret, "tid", "cid"); // missing secret
        var act = () => AuthProviderFactory.Create(opts);
        act.Should().Throw<AuthOptionsValidationException>();
    }
}

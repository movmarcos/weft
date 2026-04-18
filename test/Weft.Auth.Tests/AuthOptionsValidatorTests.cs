// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Weft.Auth;

namespace Weft.Auth.Tests;

public class AuthOptionsValidatorTests
{
    [Theory]
    [InlineData(AuthMode.Interactive)]
    [InlineData(AuthMode.DeviceCode)]
    public void Interactive_and_DeviceCode_accept_empty_TenantId(AuthMode mode)
    {
        var opts = new AuthOptions(
            Mode: mode,
            TenantId: "",
            ClientId: "some-client-id");

        Action act = () => AuthOptionsValidator.Validate(opts);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(AuthMode.ServicePrincipalSecret)]
    [InlineData(AuthMode.ServicePrincipalCertStore)]
    [InlineData(AuthMode.ServicePrincipalCertFile)]
    public void ServicePrincipal_modes_still_require_TenantId(AuthMode mode)
    {
        var opts = new AuthOptions(
            Mode: mode,
            TenantId: "",
            ClientId: "some-client-id",
            ClientSecret: mode == AuthMode.ServicePrincipalSecret ? "secret" : null,
            CertPath: mode == AuthMode.ServicePrincipalCertFile ? "/some/cert.pfx" : null,
            CertPassword: mode == AuthMode.ServicePrincipalCertFile ? "" : null,
            CertThumbprint: mode == AuthMode.ServicePrincipalCertStore ? "abc123" : null);

        Action act = () => AuthOptionsValidator.Validate(opts);
        act.Should().Throw<AuthOptionsValidationException>()
            .WithMessage("*TenantId*");
    }
}

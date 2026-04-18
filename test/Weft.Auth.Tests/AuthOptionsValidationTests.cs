// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Weft.Auth;

namespace Weft.Auth.Tests;

public class AuthOptionsValidationTests
{
    [Fact]
    public void Secret_mode_requires_ClientSecret()
    {
        var opts = new AuthOptions(AuthMode.ServicePrincipalSecret, "tid", "cid");
        var act = () => AuthOptionsValidator.Validate(opts);
        act.Should().Throw<AuthOptionsValidationException>().WithMessage("*ClientSecret*");
    }

    [Fact]
    public void CertFile_mode_requires_CertPath_and_CertPassword()
    {
        var opts = new AuthOptions(AuthMode.ServicePrincipalCertFile, "tid", "cid");
        var act = () => AuthOptionsValidator.Validate(opts);
        act.Should().Throw<AuthOptionsValidationException>().WithMessage("*CertPath*");
    }

    [Fact]
    public void CertStore_mode_requires_CertThumbprint()
    {
        var opts = new AuthOptions(AuthMode.ServicePrincipalCertStore, "tid", "cid");
        var act = () => AuthOptionsValidator.Validate(opts);
        act.Should().Throw<AuthOptionsValidationException>().WithMessage("*CertThumbprint*");
    }

    [Fact]
    public void Interactive_mode_validates_with_minimal_options()
    {
        var opts = new AuthOptions(AuthMode.Interactive, "tid", "cid");
        var act = () => AuthOptionsValidator.Validate(opts);
        act.Should().NotThrow();
    }

    [Fact]
    public void Tenant_and_client_must_be_non_empty()
    {
        var opts = new AuthOptions(AuthMode.Interactive, "", "cid");
        var act = () => AuthOptionsValidator.Validate(opts);
        act.Should().Throw<AuthOptionsValidationException>().WithMessage("*TenantId*");
    }
}

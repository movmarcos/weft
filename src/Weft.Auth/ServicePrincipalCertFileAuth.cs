// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Identity.Client;
using Weft.Core.Abstractions;

namespace Weft.Auth;

public sealed class ServicePrincipalCertFileAuth : IAuthProvider
{
    private static readonly string[] PowerBiScopes = new[] { "https://analysis.windows.net/powerbi/api/.default" };

    private readonly IConfidentialClientApplication _app;

    public ServicePrincipalCertFileAuth(AuthOptions options)
    {
        AuthOptionsValidator.Validate(options);
        if (options.Mode != AuthMode.ServicePrincipalCertFile)
            throw new ArgumentException("AuthMode must be ServicePrincipalCertFile.", nameof(options));

        var cert = CertificateLoader.LoadFromFile(options.CertPath!, options.CertPassword!);
        _app = ConfidentialClientApplicationBuilder
            .Create(options.ClientId)
            .WithCertificate(cert, sendX5C: true)
            .WithTenantId(options.TenantId)
            .Build();
    }

    public async Task<AccessToken> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var result = await _app.AcquireTokenForClient(PowerBiScopes)
            .ExecuteAsync(cancellationToken);
        return new AccessToken(result.AccessToken, result.ExpiresOn);
    }
}

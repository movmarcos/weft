// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Identity.Client;
using Weft.Core.Abstractions;

namespace Weft.Auth;

public sealed class ServicePrincipalCertStoreAuth : IAuthProvider
{
    private static readonly string[] PowerBiScopes = new[] { "https://analysis.windows.net/powerbi/api/.default" };

    private readonly IConfidentialClientApplication _app;

    public ServicePrincipalCertStoreAuth(AuthOptions options)
    {
        AuthOptionsValidator.Validate(options);
        if (options.Mode != AuthMode.ServicePrincipalCertStore)
            throw new ArgumentException("AuthMode must be ServicePrincipalCertStore.", nameof(options));

        var cert = CertificateLoader.LoadFromStore(
            options.CertThumbprint!,
            options.CertStoreLocation,
            options.CertStoreName);

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

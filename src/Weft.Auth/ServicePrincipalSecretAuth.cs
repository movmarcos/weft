// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Identity.Client;
using Weft.Core.Abstractions;

namespace Weft.Auth;

public sealed class ServicePrincipalSecretAuth : IAuthProvider
{
    private static readonly string[] PowerBiScopes = new[] { "https://analysis.windows.net/powerbi/api/.default" };

    private readonly IConfidentialClientApplication _app;

    public ServicePrincipalSecretAuth(AuthOptions options)
    {
        AuthOptionsValidator.Validate(options);
        if (options.Mode != AuthMode.ServicePrincipalSecret)
            throw new ArgumentException("AuthMode must be ServicePrincipalSecret.", nameof(options));

        _app = ConfidentialClientApplicationBuilder
            .Create(options.ClientId)
            .WithClientSecret(options.ClientSecret!)
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

// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Identity.Client;
using Weft.Core.Abstractions;

namespace Weft.Auth;

public sealed class InteractiveAuth : IAuthProvider
{
    private static readonly string[] PowerBiScopes = new[] { "https://analysis.windows.net/powerbi/api/.default" };

    private readonly IPublicClientApplication _app;

    public InteractiveAuth(AuthOptions options)
    {
        AuthOptionsValidator.Validate(options);
        if (options.Mode != AuthMode.Interactive)
            throw new ArgumentException("AuthMode must be Interactive.", nameof(options));

        var builder = PublicClientApplicationBuilder.Create(options.ClientId);

        if (string.IsNullOrWhiteSpace(options.TenantId)
            || string.Equals(options.TenantId, "common", StringComparison.OrdinalIgnoreCase))
        {
            builder = builder.WithAuthority("https://login.microsoftonline.com/common");
        }
        else
        {
            builder = builder.WithTenantId(options.TenantId);
        }

        _app = builder
            .WithRedirectUri(options.RedirectUri ?? "http://localhost")
            .Build();
    }

    public async Task<AccessToken> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var account = (await _app.GetAccountsAsync()).FirstOrDefault();
        AuthenticationResult result;
        try
        {
            result = await _app.AcquireTokenSilent(PowerBiScopes, account)
                .ExecuteAsync(cancellationToken);
        }
        catch (MsalUiRequiredException)
        {
            result = await _app.AcquireTokenInteractive(PowerBiScopes)
                .ExecuteAsync(cancellationToken);
        }
        return new AccessToken(result.AccessToken, result.ExpiresOn);
    }
}

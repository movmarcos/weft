// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Identity.Client;
using Weft.Core.Abstractions;

namespace Weft.Auth;

public sealed class DeviceCodeAuth : IAuthProvider
{
    private static readonly string[] PowerBiScopes = new[] { "https://analysis.windows.net/powerbi/api/.default" };

    private readonly IPublicClientApplication _app;
    private readonly TextWriter _instructionsOut;

    public DeviceCodeAuth(AuthOptions options, TextWriter? instructionsOut = null)
    {
        AuthOptionsValidator.Validate(options);
        if (options.Mode != AuthMode.DeviceCode)
            throw new ArgumentException("AuthMode must be DeviceCode.", nameof(options));

        _app = PublicClientApplicationBuilder
            .Create(options.ClientId)
            .WithTenantId(options.TenantId)
            .Build();
        _instructionsOut = instructionsOut ?? Console.Out;
    }

    public async Task<AccessToken> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var result = await _app.AcquireTokenWithDeviceCode(PowerBiScopes, callback =>
        {
            _instructionsOut.WriteLine(callback.Message);
            return Task.CompletedTask;
        }).ExecuteAsync(cancellationToken);

        return new AccessToken(result.AccessToken, result.ExpiresOn);
    }
}

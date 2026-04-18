// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace Weft.Auth;

public static class AuthOptionsValidator
{
    public static void Validate(AuthOptions options)
    {
        var tenantRequired =
            options.Mode is AuthMode.ServicePrincipalSecret
                        or AuthMode.ServicePrincipalCertStore
                        or AuthMode.ServicePrincipalCertFile;

        if (tenantRequired && string.IsNullOrWhiteSpace(options.TenantId))
            throw new AuthOptionsValidationException(
                "TenantId is required for service principal auth modes.");

        // Interactive/DeviceCode: empty TenantId → will use /common authority (handled in auth providers).

        if (string.IsNullOrWhiteSpace(options.ClientId))
            throw new AuthOptionsValidationException("ClientId is required.");

        switch (options.Mode)
        {
            case AuthMode.ServicePrincipalSecret:
                if (string.IsNullOrWhiteSpace(options.ClientSecret))
                    throw new AuthOptionsValidationException(
                        "ClientSecret is required for ServicePrincipalSecret mode.");
                break;

            case AuthMode.ServicePrincipalCertFile:
                if (string.IsNullOrWhiteSpace(options.CertPath))
                    throw new AuthOptionsValidationException(
                        "CertPath is required for ServicePrincipalCertFile mode.");
                if (options.CertPassword is null)
                    throw new AuthOptionsValidationException(
                        "CertPassword is required for ServicePrincipalCertFile mode (use empty string if cert is unprotected).");
                break;

            case AuthMode.ServicePrincipalCertStore:
                if (string.IsNullOrWhiteSpace(options.CertThumbprint))
                    throw new AuthOptionsValidationException(
                        "CertThumbprint is required for ServicePrincipalCertStore mode.");
                break;

            case AuthMode.Interactive:
            case AuthMode.DeviceCode:
                break;
        }
    }
}

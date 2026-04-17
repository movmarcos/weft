// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Weft.Core.Abstractions;

namespace Weft.Auth;

public static class AuthProviderFactory
{
    public static IAuthProvider Create(AuthOptions options) => options.Mode switch
    {
        AuthMode.ServicePrincipalSecret    => new ServicePrincipalSecretAuth(options),
        AuthMode.ServicePrincipalCertFile  => new ServicePrincipalCertFileAuth(options),
        AuthMode.ServicePrincipalCertStore => new ServicePrincipalCertStoreAuth(options),
        AuthMode.Interactive               => new InteractiveAuth(options),
        AuthMode.DeviceCode                => new DeviceCodeAuth(options),
        _ => throw new ArgumentOutOfRangeException(nameof(options), options.Mode, "Unknown AuthMode.")
    };
}

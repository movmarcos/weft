// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography.X509Certificates;

namespace Weft.Auth;

public sealed record AuthOptions(
    AuthMode Mode,
    string TenantId,
    string ClientId,
    string? ClientSecret = null,
    string? CertPath = null,
    string? CertPassword = null,
    string? CertThumbprint = null,
    StoreLocation CertStoreLocation = StoreLocation.LocalMachine,
    StoreName CertStoreName = StoreName.My,
    string? RedirectUri = null);

public sealed class AuthOptionsValidationException : Exception
{
    public AuthOptionsValidationException(string message) : base(message) {}
}

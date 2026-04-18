// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace Weft.Config;

public sealed record AuthConfigSection(
    string Mode,
    string TenantId,
    string ClientId,
    string? ClientSecret,
    string? CertPath,
    string? CertPassword,
    string? CertThumbprint,
    string? CertStoreLocation,
    string? CertStoreName,
    string? RedirectUri);

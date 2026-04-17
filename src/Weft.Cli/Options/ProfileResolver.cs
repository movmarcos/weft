// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Weft.Auth;

namespace Weft.Cli.Options;

public sealed record ResolvedProfile(
    string WorkspaceUrl,
    string DatabaseName,
    string SourcePath,
    string ArtifactsDirectory,
    AuthOptions Auth,
    bool AllowDrops,
    bool NoRefresh,
    bool ResetBookmarks,
    string? EffectiveDate);

public static class ProfileResolver
{
    public static AuthOptions BuildAuthOptions(
        AuthMode mode,
        string? tenant, string? client,
        string? clientSecret,
        string? certPath, string? certPassword,
        string? certThumbprint)
    {
        var t = tenant ?? Environment.GetEnvironmentVariable("WEFT_TENANT_ID")
            ?? throw new InvalidOperationException("--tenant is required (or env WEFT_TENANT_ID).");
        var c = client ?? Environment.GetEnvironmentVariable("WEFT_CLIENT_ID")
            ?? throw new InvalidOperationException("--client is required (or env WEFT_CLIENT_ID).");

        return new AuthOptions(
            Mode: mode,
            TenantId: t,
            ClientId: c,
            ClientSecret: clientSecret ?? Environment.GetEnvironmentVariable("WEFT_CLIENT_SECRET"),
            CertPath: certPath ?? Environment.GetEnvironmentVariable("WEFT_CERT_PATH"),
            CertPassword: certPassword ?? Environment.GetEnvironmentVariable("WEFT_CERT_PASSWORD"),
            CertThumbprint: certThumbprint ?? Environment.GetEnvironmentVariable("WEFT_CERT_THUMBPRINT"));
    }
}

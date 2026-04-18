// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Weft.Auth;
using Weft.Config;
using Weft.Core.Parameters;

namespace Weft.Cli.Options;

public sealed record ResolvedProfile(
    string ProfileName,
    string WorkspaceUrl,
    string DatabaseName,
    string SourcePath,
    string ArtifactsDirectory,
    AuthOptions Auth,
    RefreshConfigSection Refresh,
    bool AllowDrops,
    bool AllowHistoryLoss,
    bool NoRefresh,
    bool ResetBookmarks,
    string? EffectiveDate,
    IReadOnlyDictionary<string, object?> ParameterValues,
    IReadOnlyDictionary<string, string>? ParameterCliOverrides,
    IReadOnlyList<ParameterDeclaration> ParameterDeclarations,
    HooksConfigSection Hooks,
    int TimeoutMinutes);

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

    public static ResolvedProfile Build(
        WeftConfig? config,
        string profileName,
        string sourcePath,
        string artifactsDirectory,
        bool noRefresh,
        bool resetBookmarks,
        string? effectiveDate,
        Dictionary<string, string>? cliParameters,
        string? workspaceOverride = null,
        string? databaseOverride = null,
        AuthMode? authModeOverride = null,
        string? tenantOverride = null,
        string? clientOverride = null,
        string? clientSecretOverride = null,
        string? certPathOverride = null,
        string? certPasswordOverride = null,
        string? certThumbprintOverride = null)
    {
        EffectiveProfileConfig effective;
        if (config is null)
        {
            var auth = BuildAuthOptions(
                authModeOverride ?? AuthMode.Interactive, tenantOverride, clientOverride,
                clientSecretOverride, certPathOverride, certPasswordOverride, certThumbprintOverride);
            effective = new EffectiveProfileConfig(
                ProfileName: profileName,
                Workspace: workspaceOverride ?? throw new InvalidOperationException("--workspace required without --config."),
                Database: databaseOverride ?? throw new InvalidOperationException("--database required without --config."),
                Auth: new AuthConfigSection(
                    auth.Mode.ToString(),
                    auth.TenantId,
                    auth.ClientId,
                    auth.ClientSecret,
                    auth.CertPath,
                    auth.CertPassword,
                    auth.CertThumbprint,
                    auth.CertStoreLocation.ToString(),
                    auth.CertStoreName.ToString(),
                    auth.RedirectUri),
                Refresh: new RefreshConfigSection(
                    "full", 10, 15,
                    new IncrementalPolicyConfig(true, true, "preserve"),
                    new DynamicPartitionStrategyConfig("newestOnly", 1)),
                AllowDrops: false,
                AllowHistoryLoss: false,
                TimeoutMinutes: 60,
                Parameters: new Dictionary<string, object?>(),
                Hooks: new HooksConfigSection(null, null, null, null, null, null));
        }
        else
        {
            effective = new ProfileMerger().Merge(config, profileName);
        }

        var authOptions = BuildAuthOptionsFromSection(effective.Auth, authModeOverride,
            clientSecretOverride, certPathOverride, certPasswordOverride, certThumbprintOverride);

        return new ResolvedProfile(
            ProfileName: effective.ProfileName,
            WorkspaceUrl: workspaceOverride ?? effective.Workspace,
            DatabaseName: databaseOverride ?? effective.Database,
            SourcePath: sourcePath,
            ArtifactsDirectory: artifactsDirectory,
            Auth: authOptions,
            Refresh: effective.Refresh,
            AllowDrops: effective.AllowDrops,
            AllowHistoryLoss: effective.AllowHistoryLoss,
            NoRefresh: noRefresh,
            ResetBookmarks: resetBookmarks,
            EffectiveDate: effectiveDate,
            ParameterValues: effective.Parameters,
            ParameterCliOverrides: cliParameters,
            ParameterDeclarations: config?.Parameters ?? Array.Empty<ParameterDeclaration>(),
            Hooks: effective.Hooks,
            TimeoutMinutes: effective.TimeoutMinutes);
    }

    private static AuthOptions BuildAuthOptionsFromSection(
        AuthConfigSection section,
        AuthMode? overrideMode,
        string? secretOverride, string? certPathOverride,
        string? certPasswordOverride, string? certThumbprintOverride)
    {
        var mode = overrideMode ?? Enum.Parse<AuthMode>(section.Mode);
        return new AuthOptions(
            Mode: mode,
            TenantId: EnvVarExpander.Expand(section.TenantId) ?? "",
            ClientId: EnvVarExpander.Expand(section.ClientId) ?? "",
            ClientSecret: secretOverride ?? EnvVarExpander.Expand(section.ClientSecret),
            CertPath: certPathOverride ?? EnvVarExpander.Expand(section.CertPath),
            CertPassword: certPasswordOverride ?? EnvVarExpander.Expand(section.CertPassword),
            CertThumbprint: certThumbprintOverride ?? EnvVarExpander.Expand(section.CertThumbprint),
            CertStoreLocation: Enum.TryParse<System.Security.Cryptography.X509Certificates.StoreLocation>(
                section.CertStoreLocation, out var loc) ? loc : System.Security.Cryptography.X509Certificates.StoreLocation.LocalMachine,
            CertStoreName: Enum.TryParse<System.Security.Cryptography.X509Certificates.StoreName>(
                section.CertStoreName, out var nm) ? nm : System.Security.Cryptography.X509Certificates.StoreName.My,
            RedirectUri: section.RedirectUri);
    }
}

// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using Weft.Auth;

namespace Weft.Cli.Options;

public static class CommonOptions
{
    public static Option<string> SourceOption() =>
        new("--source", "-s") { Description = "Path to source .bim or TE folder.", Required = true };

    public static Option<string> WorkspaceOption() =>
        new("--workspace", "-w") { Description = "XMLA workspace URL (powerbi://...)", Required = true };

    public static Option<string> DatabaseOption() =>
        new("--database", "-d") { Description = "Target dataset/database name.", Required = true };

    public static Option<string> ArtifactsOption() =>
        new("--artifacts") { Description = "Directory for plan/manifest/receipt JSON.", DefaultValueFactory = _ => "./artifacts" };

    /// <summary>Reserved for Plan 3; commands currently write directly to Console.</summary>
    public static Option<string> LogFormatOption() =>
        new("--log-format") { Description = "human | json", DefaultValueFactory = _ => "human" };

    public static Option<bool> AllowDropsOption() =>
        new("--allow-drops") { Description = "Permit dropping tables that exist on target but not source." };

    public static Option<bool> NoRefreshOption() =>
        new("--no-refresh") { Description = "Skip refresh after deploy." };

    public static Option<bool> ResetBookmarksOption() =>
        new("--reset-bookmarks") { Description = "Clear RefreshBookmark annotations on refreshed tables before refresh." };

    public static Option<string?> EffectiveDateOption() =>
        new("--effective-date") { Description = "ISO date used as RefreshPolicy effectiveDate (UTC)." };

    public static Option<AuthMode> AuthModeOption() =>
        new("--auth") { Description = "Auth mode.", DefaultValueFactory = _ => AuthMode.Interactive };

    public static Option<string?> TenantOption() =>
        new("--tenant") { Description = "AAD tenant id (or env: WEFT_TENANT_ID)." };

    public static Option<string?> ClientOption() =>
        new("--client") { Description = "AAD client id (or env: WEFT_CLIENT_ID)." };

    public static Option<string?> ClientSecretOption() =>
        new("--client-secret") { Description = "(secret mode) Client secret (or env: WEFT_CLIENT_SECRET)." };

    public static Option<string?> CertPathOption() =>
        new("--cert-path") { Description = "(cert-file mode) Path to .pfx (or env: WEFT_CERT_PATH)." };

    public static Option<string?> CertPasswordOption() =>
        new("--cert-password") { Description = "(cert-file mode) .pfx password (or env: WEFT_CERT_PASSWORD)." };

    public static Option<string?> CertThumbprintOption() =>
        new("--cert-thumbprint") { Description = "(cert-store mode) Cert thumbprint (or env: WEFT_CERT_THUMBPRINT)." };
}

// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using Weft.Auth;
using Weft.Cli.Options;
using Weft.Config;
using Weft.Xmla;

namespace Weft.Cli.Commands;

public static class RestoreHistoryCommand
{
    public static Command Build()
    {
        var workspace = CommonOptions.WorkspaceOption();
        var database  = CommonOptions.DatabaseOption();
        var table     = new Option<string>("--table") { Description = "Table to restore.", Required = true };
        var fromOpt   = new Option<string?>("--from")  { Description = "ISO date (inclusive)." };
        var toOpt     = new Option<string?>("--to")    { Description = "ISO date (inclusive)." };
        var effectiveDate = CommonOptions.EffectiveDateOption();
        var authMode  = CommonOptions.AuthModeOption();
        var tenant    = CommonOptions.TenantOption();
        var client    = CommonOptions.ClientOption();
        var clientSecret = CommonOptions.ClientSecretOption();
        var certPath  = CommonOptions.CertPathOption();
        var certPwd   = CommonOptions.CertPasswordOption();
        var certThumb = CommonOptions.CertThumbprintOption();
        var configOpt = CommonOptions.ConfigFileOption();
        var targetOpt = CommonOptions.TargetProfileOption();

        // Override Required = true so they become optional when --config + --target are provided
        workspace.Required = false;
        database.Required  = false;

        var cmd = new Command("restore-history", "Re-materialize historical partitions per the table's RefreshPolicy.");
        foreach (var o in new Option[] { workspace, database, table, fromOpt, toOpt, effectiveDate,
                                         authMode, tenant, client, clientSecret, certPath, certPwd, certThumb })
            cmd.Options.Add(o);
        cmd.Options.Add(configOpt);
        cmd.Options.Add(targetOpt);

        cmd.SetAction(async (parse, ct) =>
        {
            // Load config if present
            WeftConfig? config = null;
            var configPath = parse.GetValue(configOpt);
            if (!string.IsNullOrEmpty(configPath))
            {
                try { config = YamlConfigLoader.LoadFromFile(configPath); }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Config load failed: {ex.Message}");
                    return ExitCodes.ConfigError;
                }
            }

            string workspaceUrl; string databaseName; AuthOptions authOpts;
            var targetName = parse.GetValue(targetOpt);
            if (config != null && !string.IsNullOrEmpty(targetName))
            {
                EffectiveProfileConfig eff;
                try { eff = new ProfileMerger().Merge(config, targetName); }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Profile resolution failed: {ex.Message}");
                    return ExitCodes.ConfigError;
                }
                workspaceUrl = parse.GetValue(workspace) ?? eff.Workspace;
                databaseName = parse.GetValue(database)  ?? eff.Database;
                var mode = parse.GetValue(authMode) ?? Enum.Parse<AuthMode>(eff.Auth.Mode);
                authOpts = new AuthOptions(
                    Mode: mode,
                    TenantId: EnvVarExpander.Expand(eff.Auth.TenantId) ?? "",
                    ClientId: EnvVarExpander.Expand(eff.Auth.ClientId) ?? "",
                    ClientSecret: parse.GetValue(clientSecret) ?? EnvVarExpander.Expand(eff.Auth.ClientSecret),
                    CertPath: parse.GetValue(certPath) ?? EnvVarExpander.Expand(eff.Auth.CertPath),
                    CertPassword: parse.GetValue(certPwd) ?? EnvVarExpander.Expand(eff.Auth.CertPassword),
                    CertThumbprint: parse.GetValue(certThumb) ?? EnvVarExpander.Expand(eff.Auth.CertThumbprint));
            }
            else
            {
                workspaceUrl = parse.GetValue(workspace)
                    ?? throw new InvalidOperationException("--workspace required without --config + --target.");
                databaseName = parse.GetValue(database)
                    ?? throw new InvalidOperationException("--database required without --config + --target.");
                authOpts = ProfileResolver.BuildAuthOptions(
                    parse.GetValue(authMode) ?? AuthMode.Interactive,
                    parse.GetValue(tenant), parse.GetValue(client),
                    parse.GetValue(clientSecret), parse.GetValue(certPath),
                    parse.GetValue(certPwd), parse.GetValue(certThumb));
            }

            var provider = AuthProviderFactory.Create(authOpts);
            var token = await provider.GetTokenAsync(ct);

            var tableName = parse.GetValue(table)!;
            var entries = new[]
            {
                new RefreshTableEntry(tableName,
                    new RefreshTypeSpec(RefreshTypeSpec.RefreshKind.Policy, ApplyRefreshPolicy: true))
            };
            var tmsl = new RefreshCommandBuilder().Build(
                databaseName,
                entries,
                parse.GetValue(effectiveDate) ?? parse.GetValue(toOpt));

            Console.Out.WriteLine(
                $"Restoring history for '{tableName}' from {parse.GetValue(fromOpt) ?? "<policy start>"} to {parse.GetValue(toOpt) ?? "<today>"}.");
            Console.Out.WriteLine("WARNING: this can only recover data the source system still has.");

            var result = await new XmlaExecutor().ExecuteAsync(workspaceUrl, databaseName, token, tmsl, ct);
            foreach (var m in result.Messages) Console.Out.WriteLine(m);
            return result.Success ? ExitCodes.Success : ExitCodes.RefreshError;
        });
        return cmd;
    }
}

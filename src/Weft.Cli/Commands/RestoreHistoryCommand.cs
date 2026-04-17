// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using Weft.Auth;
using Weft.Cli.Options;
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

        var cmd = new Command("restore-history", "Re-materialize historical partitions per the table's RefreshPolicy.");
        foreach (var o in new Option[] { workspace, database, table, fromOpt, toOpt, effectiveDate,
                                         authMode, tenant, client, clientSecret, certPath, certPwd, certThumb })
            cmd.Options.Add(o);

        cmd.SetAction(async (parse, ct) =>
        {
            var auth = ProfileResolver.BuildAuthOptions(
                parse.GetValue(authMode), parse.GetValue(tenant), parse.GetValue(client),
                parse.GetValue(clientSecret), parse.GetValue(certPath), parse.GetValue(certPwd),
                parse.GetValue(certThumb));
            var provider = AuthProviderFactory.Create(auth);
            var token = await provider.GetTokenAsync(ct);

            var tableName = parse.GetValue(table)!;
            var entries = new[]
            {
                new RefreshTableEntry(tableName,
                    new RefreshTypeSpec(RefreshTypeSpec.RefreshKind.Policy, ApplyRefreshPolicy: true))
            };
            var tmsl = new RefreshCommandBuilder().Build(
                parse.GetValue(database)!,
                entries,
                parse.GetValue(effectiveDate) ?? parse.GetValue(toOpt));

            Console.Out.WriteLine(
                $"Restoring history for '{tableName}' from {parse.GetValue(fromOpt) ?? "<policy start>"} to {parse.GetValue(toOpt) ?? "<today>"}.");
            Console.Out.WriteLine("WARNING: this can only recover data the source system still has.");

            var result = await new XmlaExecutor().ExecuteAsync(
                parse.GetValue(workspace)!, parse.GetValue(database)!, token, tmsl, ct);
            foreach (var m in result.Messages) Console.Out.WriteLine(m);
            return result.Success ? ExitCodes.Success : ExitCodes.RefreshError;
        });
        return cmd;
    }
}

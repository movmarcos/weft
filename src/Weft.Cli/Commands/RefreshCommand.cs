// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using Weft.Auth;
using Weft.Cli.Options;
using Weft.Xmla;

namespace Weft.Cli.Commands;

public static class RefreshCommand
{
    public static Command Build()
    {
        var workspace = CommonOptions.WorkspaceOption();
        var database  = CommonOptions.DatabaseOption();
        var tables    = new Option<string>("--tables")
            { Description = "Comma-separated table names to refresh.", Required = true };
        var effectiveDate = CommonOptions.EffectiveDateOption();
        var authMode  = CommonOptions.AuthModeOption();
        var tenant    = CommonOptions.TenantOption();
        var client    = CommonOptions.ClientOption();
        var clientSecret = CommonOptions.ClientSecretOption();
        var certPath  = CommonOptions.CertPathOption();
        var certPwd   = CommonOptions.CertPasswordOption();
        var certThumb = CommonOptions.CertThumbprintOption();

        var cmd = new Command("refresh", "Refresh selected tables.");
        foreach (var o in new Option[] { workspace, database, tables, effectiveDate,
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

            var names = parse.GetValue(tables)!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var entries = names.Select(n => new RefreshTableEntry(
                n, new RefreshTypeSpec(RefreshTypeSpec.RefreshKind.Full, ApplyRefreshPolicy: false))).ToList();
            var tmsl = new RefreshCommandBuilder().Build(parse.GetValue(database)!, entries, parse.GetValue(effectiveDate));
            var result = await new XmlaExecutor().ExecuteAsync(
                parse.GetValue(workspace)!, parse.GetValue(database)!, token, tmsl, ct);

            foreach (var m in result.Messages) Console.Out.WriteLine(m);
            return result.Success ? ExitCodes.Success : ExitCodes.RefreshError;
        });
        return cmd;
    }
}

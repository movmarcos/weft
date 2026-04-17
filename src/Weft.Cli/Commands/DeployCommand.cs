// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.Text.Json;
using Weft.Auth;
using Weft.Cli.Options;
using Weft.Core;
using Weft.Core.Abstractions;
using Weft.Core.Loading;
using Weft.Core.Partitions;
using Weft.Core.Tmsl;
using Weft.Xmla;

namespace Weft.Cli.Commands;

public static class DeployCommand
{
    public static Command Build()
    {
        var src       = CommonOptions.SourceOption();
        var workspace = CommonOptions.WorkspaceOption();
        var database  = CommonOptions.DatabaseOption();
        var artifacts = CommonOptions.ArtifactsOption();
        var allowDrops = CommonOptions.AllowDropsOption();
        var noRefresh  = CommonOptions.NoRefreshOption();
        var resetBookmarks = CommonOptions.ResetBookmarksOption();
        var effectiveDate  = CommonOptions.EffectiveDateOption();
        var authMode  = CommonOptions.AuthModeOption();
        var tenant    = CommonOptions.TenantOption();
        var client    = CommonOptions.ClientOption();
        var clientSecret = CommonOptions.ClientSecretOption();
        var certPath  = CommonOptions.CertPathOption();
        var certPwd   = CommonOptions.CertPasswordOption();
        var certThumb = CommonOptions.CertThumbprintOption();

        var cmd = new Command("deploy", "Deploy a model: load → diff → execute → refresh.");
        foreach (var o in new Option[] { src, workspace, database, artifacts, allowDrops, noRefresh,
                                         resetBookmarks, effectiveDate, authMode, tenant, client,
                                         clientSecret, certPath, certPwd, certThumb })
            cmd.Options.Add(o);

        cmd.SetAction(async (parse, ct) =>
        {
            var auth = ProfileResolver.BuildAuthOptions(
                parse.GetValue(authMode), parse.GetValue(tenant), parse.GetValue(client),
                parse.GetValue(clientSecret), parse.GetValue(certPath), parse.GetValue(certPwd),
                parse.GetValue(certThumb));
            var provider = AuthProviderFactory.Create(auth);

            return await RunAsync(
                source: parse.GetValue(src)!,
                workspaceUrl: parse.GetValue(workspace)!,
                databaseName: parse.GetValue(database)!,
                artifactsDirectory: parse.GetValue(artifacts)!,
                allowDrops: parse.GetValue(allowDrops),
                noRefresh: parse.GetValue(noRefresh),
                resetBookmarks: parse.GetValue(resetBookmarks),
                effectiveDate: parse.GetValue(effectiveDate),
                auth: provider,
                targetReader: new TargetReader(),
                executor: new XmlaExecutor(),
                refreshRunner: new RefreshRunner(new XmlaExecutor()),
                manifestStore: new FilePartitionManifestStore(),
                cancellationToken: ct);
        });
        return cmd;
    }

    public static async Task<int> RunAsync(
        string source,
        string workspaceUrl,
        string databaseName,
        string artifactsDirectory,
        bool allowDrops,
        bool noRefresh,
        bool resetBookmarks,
        string? effectiveDate,
        IAuthProvider auth,
        ITargetReader targetReader,
        IXmlaExecutor executor,
        IRefreshRunner refreshRunner,
        IPartitionManifestStore manifestStore,
        CancellationToken cancellationToken = default)
    {
        _ = resetBookmarks; // reserved for upcoming reset-bookmarks wiring (Plan 2 Task 26+).

        // 1. Auth
        AccessToken token;
        try { token = await auth.GetTokenAsync(cancellationToken); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Auth failed: {ex.Message}");
            return ExitCodes.AuthError;
        }

        // 2. Load source
        Microsoft.AnalysisServices.Tabular.Database srcDb;
        try { srcDb = ModelLoaderFactory.For(source).Load(source); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Source load failed: {ex.Message}");
            return ExitCodes.SourceLoadError;
        }

        // 3. Read target + write pre-manifest
        Microsoft.AnalysisServices.Tabular.Database tgtDb;
        try { tgtDb = await targetReader.ReadAsync(workspaceUrl, databaseName, token, cancellationToken); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Target read failed: {ex.Message}");
            return ExitCodes.TargetReadError;
        }
        var preManifest = new PartitionManifestReader().Read(tgtDb);
        var prePath = manifestStore.Write(preManifest, artifactsDirectory, "pre-partitions");
        Console.Out.WriteLine($"Pre-deploy manifest: {prePath}");

        // 4. Plan
        PlanResult plan;
        try { plan = WeftCore.Plan(srcDb, tgtDb); }
        catch (PartitionIntegrityException ex)
        {
            Console.Error.WriteLine($"Partition integrity violation: {ex.Message}");
            return ExitCodes.PartitionIntegrityError;
        }

        // 5. Pre-flight: drops
        if (plan.ChangeSet.TablesToDrop.Count > 0 && !allowDrops)
        {
            Console.Error.WriteLine(
                $"Refusing to drop tables without --allow-drops: {string.Join(", ", plan.ChangeSet.TablesToDrop)}");
            return ExitCodes.DiffValidationError;
        }

        if (plan.ChangeSet.IsEmpty)
        {
            Console.Out.WriteLine("Nothing to deploy.");
        }

        // 6. Write plan TMSL
        Directory.CreateDirectory(artifactsDirectory);
        var ts = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var planPath = Path.Combine(artifactsDirectory, $"{ts}-{databaseName}-plan.tmsl");
        await File.WriteAllTextAsync(planPath, plan.TmslJson, cancellationToken);

        // 7. Execute (skip if empty)
        if (!plan.ChangeSet.IsEmpty)
        {
            var exec = await executor.ExecuteAsync(workspaceUrl, databaseName, token, plan.TmslJson, cancellationToken);
            foreach (var m in exec.Messages) Console.Out.WriteLine(m);
            if (!exec.Success)
            {
                Console.Error.WriteLine("TMSL execution failed.");
                return ExitCodes.TmslExecutionError;
            }
        }

        // 8. Post-deploy manifest + integrity gate
        var postDb = await targetReader.ReadAsync(workspaceUrl, databaseName, token, cancellationToken);
        var postManifest = new PartitionManifestReader().Read(postDb);
        var postPath = manifestStore.Write(postManifest, artifactsDirectory, "post-partitions");
        Console.Out.WriteLine($"Post-deploy manifest: {postPath}");

        var droppedTables = new HashSet<string>(plan.ChangeSet.TablesToDrop, StringComparer.Ordinal);
        foreach (var (table, prePartitions) in preManifest.Tables)
        {
            if (droppedTables.Contains(table)) continue;
            if (!postManifest.Tables.TryGetValue(table, out var postPartitions))
            {
                Console.Error.WriteLine($"Partition integrity violation: table '{table}' missing post-deploy.");
                return ExitCodes.PartitionIntegrityError;
            }
            var postNames = postPartitions.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
            var missing = prePartitions.Where(p => !postNames.Contains(p.Name)).Select(p => p.Name).ToList();
            if (missing.Count > 0)
            {
                Console.Error.WriteLine(
                    $"Partition integrity violation on '{table}': missing post-deploy: {string.Join(", ", missing)}");
                return ExitCodes.PartitionIntegrityError;
            }
        }

        // 9. Refresh
        if (!noRefresh && !plan.ChangeSet.IsEmpty)
        {
            var req = new RefreshRequest(workspaceUrl, databaseName, token, plan.ChangeSet, effectiveDate);
            var rrx = await refreshRunner.RefreshAsync(req,
                progress: new Progress<string>(line => Console.Out.WriteLine(line)),
                cancellationToken: cancellationToken);
            if (!rrx.Success)
            {
                Console.Error.WriteLine("Refresh failed (deploy succeeded). Investigate.");
                return ExitCodes.RefreshError;
            }
        }

        // 10. Receipt
        var receipt = new
        {
            ts,
            databaseName,
            workspaceUrl,
            add = plan.ChangeSet.TablesToAdd.Select(t => t.Name).ToArray(),
            drop = plan.ChangeSet.TablesToDrop.ToArray(),
            alter = plan.ChangeSet.TablesToAlter.Select(t => t.Name).ToArray(),
            unchanged = plan.ChangeSet.TablesUnchanged.ToArray(),
            preManifest = prePath,
            postManifest = postPath,
            planTmsl = planPath,
            refreshSkipped = noRefresh,
        };
        var receiptPath = Path.Combine(artifactsDirectory, $"{ts}-{databaseName}-receipt.json");
        await File.WriteAllTextAsync(
            receiptPath,
            JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
        Console.Out.WriteLine($"Receipt: {receiptPath}");

        return ExitCodes.Success;
    }
}

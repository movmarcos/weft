// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.Text.Json;
using Weft.Auth;
using Weft.Cli.Auth;
using Weft.Cli.Options;
using Weft.Cli.Output;
using Weft.Config;
using Weft.Core;
using Weft.Core.Abstractions;
using Weft.Core.Diffing;
using Weft.Core.Hooks;
using Weft.Core.Loading;
using Weft.Core.Parameters;
using Weft.Core.Partitions;
using Weft.Core.RefreshPolicy;
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
        var logFormatOpt = CommonOptions.LogFormatOption();

        var configOpt = new Option<string?>("--config") { Description = "Path to weft.yaml." };
        var targetOpt = new Option<string?>("--target") { Description = "Profile name in weft.yaml." };

        var cmd = new Command("deploy", "Deploy a model: load → diff → execute → refresh.");
        foreach (var o in new Option[] { src, workspace, database, artifacts, allowDrops, noRefresh,
                                         resetBookmarks, effectiveDate, authMode, tenant, client,
                                         clientSecret, certPath, certPwd, certThumb })
            cmd.Options.Add(o);
        cmd.Options.Add(configOpt);
        cmd.Options.Add(targetOpt);
        cmd.Options.Add(logFormatOpt);

        cmd.SetAction(async (parse, ct) =>
        {
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

            ResolvedProfile profile;
            try
            {
                profile = ProfileResolver.Build(
                    config: config,
                    profileName: parse.GetValue(targetOpt) ?? "default",
                    sourcePath: parse.GetValue(src) ?? config?.Source?.Path ?? "",
                    artifactsDirectory: parse.GetValue(artifacts)!,
                    noRefresh: parse.GetValue(noRefresh),
                    resetBookmarks: parse.GetValue(resetBookmarks),
                    effectiveDate: parse.GetValue(effectiveDate),
                    cliParameters: null,
                    workspaceOverride: parse.GetValue(workspace),
                    databaseOverride: parse.GetValue(database),
                    authModeOverride: parse.GetValue(authMode),
                    tenantOverride: parse.GetValue(tenant),
                    clientOverride: parse.GetValue(client),
                    clientSecretOverride: parse.GetValue(clientSecret),
                    certPathOverride: parse.GetValue(certPath),
                    certPasswordOverride: parse.GetValue(certPwd),
                    certThumbprintOverride: parse.GetValue(certThumb));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Profile resolution failed: {ex.Message}");
                return ExitCodes.ConfigError;
            }

            var logFormat = parse.GetValue(logFormatOpt) ?? "human";
            IConsoleWriter writer = logFormat.Equals("json", StringComparison.OrdinalIgnoreCase)
                ? new JsonConsoleWriter()
                : new HumanConsoleWriter();

            var innerAuth = AuthProviderFactory.Create(profile.Auth);
            var tokenMgr = new TokenManager(innerAuth, TimeSpan.FromMinutes(30));
            var sharedExecutor = new XmlaExecutor();

            return await RunAsync(
                profile,
                tokenMgr,
                new TargetReader(),
                sharedExecutor,
                new RefreshRunner(sharedExecutor),
                new FilePartitionManifestStore(),
                ct,
                writer);
        });
        return cmd;
    }

    public static async Task<int> RunAsync(
        ResolvedProfile profile,
        IAuthProvider auth,
        ITargetReader targetReader,
        IXmlaExecutor executor,
        IRefreshRunner refreshRunner,
        IPartitionManifestStore manifestStore,
        CancellationToken cancellationToken = default,
        IConsoleWriter? writer = null)
    {
        writer ??= new HumanConsoleWriter();

        // 1. Auth
        AccessToken token;
        try { token = await auth.GetTokenAsync(cancellationToken); }
        catch (Exception ex) { writer.Error($"Auth failed: {ex.Message}"); return ExitCodes.AuthError; }

        // 2. Load source
        Microsoft.AnalysisServices.Tabular.Database srcDb;
        try { srcDb = ModelLoaderFactory.For(profile.SourcePath).Load(profile.SourcePath); }
        catch (Exception ex) { writer.Error($"Source load failed: {ex.Message}"); return ExitCodes.SourceLoadError; }

        // 2a. Apply parameters
        try
        {
            var resolver = new ParameterResolver();
            var resolutions = resolver.Resolve(
                srcDb,
                profile.ParameterDeclarations,
                profile.ParameterValues,
                profile.ParameterCliOverrides,
                paramsFileValues: null);
            resolver.Apply(srcDb, resolutions);
        }
        catch (ParameterApplicationException ex)
        {
            writer.Error($"Parameter resolution failed: {ex.Message}");
            return ExitCodes.ParameterError;
        }

        // 3. Read target + pre-manifest
        Microsoft.AnalysisServices.Tabular.Database tgtDb;
        try { tgtDb = await targetReader.ReadAsync(profile.WorkspaceUrl, profile.DatabaseName, token, cancellationToken); }
        catch (Exception ex) { writer.Error($"Target read failed: {ex.Message}"); return ExitCodes.TargetReadError; }

        var preManifest = new PartitionManifestReader().Read(tgtDb);
        var prePath = manifestStore.Write(preManifest, profile.ArtifactsDirectory, "pre-partitions");
        writer.Info($"Pre-deploy manifest: {prePath}");

        // 4. Plan
        PlanResult plan;
        try { plan = WeftCore.Plan(srcDb, tgtDb); }
        catch (PartitionIntegrityException ex)
        {
            writer.Error($"Partition integrity violation: {ex.Message}");
            return ExitCodes.PartitionIntegrityError;
        }

        // 5. Pre-flight: drops
        if (plan.ChangeSet.TablesToDrop.Count > 0 && !profile.AllowDrops)
        {
            writer.Error(
                $"Refusing to drop tables without allowDrops: {string.Join(", ", plan.ChangeSet.TablesToDrop)}");
            return ExitCodes.DiffValidationError;
        }

        // 5a. Pre-flight: history-loss
        var gate = new HistoryLossGate(new RetentionCalculator());
        var historyViolations = gate.Check(plan.ChangeSet, tgtDb, profile.AllowHistoryLoss);
        if (historyViolations.Count > 0)
        {
            foreach (var v in historyViolations)
                writer.Error(
                    $"History-loss violation on {v.TableName}: would remove {string.Join(", ", v.LostPartitions)}");
            writer.Error("Set allowHistoryLoss: true in the profile to proceed.");
            return ExitCodes.DiffValidationError;
        }

        // 6. pre-plan hook
        await RunHookAsync(profile.Hooks.PrePlan, HookPhase.PrePlan, profile, plan.ChangeSet, writer);

        if (plan.ChangeSet.IsEmpty)
            writer.Info("Nothing to deploy.");

        // 7. Write plan TMSL
        Directory.CreateDirectory(profile.ArtifactsDirectory);
        var ts = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var planPath = Path.Combine(profile.ArtifactsDirectory, $"{ts}-{profile.DatabaseName}-plan.tmsl");
        await File.WriteAllTextAsync(planPath, plan.TmslJson, cancellationToken);

        // 8. pre-deploy hook + execute
        await RunHookAsync(profile.Hooks.PreDeploy, HookPhase.PreDeploy, profile, plan.ChangeSet, writer);

        if (!plan.ChangeSet.IsEmpty)
        {
            var exec = await executor.ExecuteAsync(
                profile.WorkspaceUrl, profile.DatabaseName, token, plan.TmslJson, cancellationToken);
            foreach (var m in exec.Messages) writer.Info(m);
            if (!exec.Success)
            {
                await RunHookAsync(profile.Hooks.OnFailure, HookPhase.OnFailure, profile, plan.ChangeSet, writer);
                writer.Error("TMSL execution failed.");
                return ExitCodes.TmslExecutionError;
            }
        }

        // 9. Post-deploy manifest + integrity gate
        var postDb = await targetReader.ReadAsync(profile.WorkspaceUrl, profile.DatabaseName, token, cancellationToken);
        var postManifest = new PartitionManifestReader().Read(postDb);
        var postPath = manifestStore.Write(postManifest, profile.ArtifactsDirectory, "post-partitions");
        writer.Info($"Post-deploy manifest: {postPath}");

        var droppedTables = new HashSet<string>(plan.ChangeSet.TablesToDrop, StringComparer.Ordinal);
        foreach (var (tableName, prePartitions) in preManifest.Tables)
        {
            if (droppedTables.Contains(tableName)) continue;
            if (!postManifest.Tables.TryGetValue(tableName, out var postPartitions))
            {
                await RunHookAsync(profile.Hooks.OnFailure, HookPhase.OnFailure, profile, plan.ChangeSet, writer);
                writer.Error($"Partition integrity violation: table '{tableName}' missing post-deploy.");
                return ExitCodes.PartitionIntegrityError;
            }
            var postNames = postPartitions.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
            var missing = prePartitions.Where(p => !postNames.Contains(p.Name)).Select(p => p.Name).ToList();
            if (missing.Count > 0)
            {
                await RunHookAsync(profile.Hooks.OnFailure, HookPhase.OnFailure, profile, plan.ChangeSet, writer);
                writer.Error(
                    $"Partition integrity violation on '{tableName}': missing post-deploy: {string.Join(", ", missing)}");
                return ExitCodes.PartitionIntegrityError;
            }
        }

        // 10. Bookmark clearing + pre-refresh hook + refresh
        await RunHookAsync(profile.Hooks.PreRefresh, HookPhase.PreRefresh, profile, plan.ChangeSet, writer);

        if (!profile.NoRefresh && !plan.ChangeSet.IsEmpty)
        {
            var bookmarkMode = profile.ResetBookmarks ? "clearAll"
                : profile.Refresh.IncrementalPolicy?.BookmarkMode ?? "preserve";
            if (bookmarkMode != "preserve")
            {
                var tablesToClear = bookmarkMode switch
                {
                    "clearAll" => plan.ChangeSet.RefreshTargets.ToList(),
                    "clearForPolicyChange" => plan.ChangeSet.TablesToAlter
                        .Where(d => d.RefreshPolicyChanged).Select(d => d.Name).ToList(),
                    _ => new List<string>()
                };
                if (tablesToClear.Count > 0)
                {
                    var clearTmsl = new BookmarkClearer().BuildTmsl(postDb, tablesToClear);
                    using var clearTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    clearTimeoutCts.CancelAfter(TimeSpan.FromMinutes(profile.TimeoutMinutes));
                    var clearRes = await executor.ExecuteAsync(
                        profile.WorkspaceUrl, profile.DatabaseName, token, clearTmsl, clearTimeoutCts.Token);
                    if (!clearRes.Success)
                    {
                        await RunHookAsync(profile.Hooks.OnFailure, HookPhase.OnFailure, profile, plan.ChangeSet, writer);
                        writer.Error("Bookmark clearing failed.");
                        return ExitCodes.RefreshError;
                    }
                }
            }

            var req = new RefreshRequest(profile.WorkspaceUrl, profile.DatabaseName, token, plan.ChangeSet, profile.EffectiveDate);
            using var refreshTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            refreshTimeoutCts.CancelAfter(TimeSpan.FromMinutes(profile.TimeoutMinutes));

            var rrx = await refreshRunner.RefreshAsync(req,
                progress: new Progress<string>(line => writer.Info(line)),
                cancellationToken: refreshTimeoutCts.Token);
            if (!rrx.Success)
            {
                await RunHookAsync(profile.Hooks.OnFailure, HookPhase.OnFailure, profile, plan.ChangeSet, writer);
                writer.Error("Refresh failed.");
                return ExitCodes.RefreshError;
            }
        }

        await RunHookAsync(profile.Hooks.PostRefresh, HookPhase.PostRefresh, profile, plan.ChangeSet, writer);
        await RunHookAsync(profile.Hooks.PostDeploy, HookPhase.PostDeploy, profile, plan.ChangeSet, writer);

        // 11. Receipt
        var receipt = new
        {
            ts, profile.DatabaseName, profile.WorkspaceUrl, profile.ProfileName,
            add = plan.ChangeSet.TablesToAdd.Select(t => t.Name).ToArray(),
            drop = plan.ChangeSet.TablesToDrop.ToArray(),
            alter = plan.ChangeSet.TablesToAlter.Select(t => t.Name).ToArray(),
            unchanged = plan.ChangeSet.TablesUnchanged.ToArray(),
            preManifest = prePath,
            postManifest = postPath,
            planTmsl = planPath,
            refreshSkipped = profile.NoRefresh
        };
        var receiptPath = Path.Combine(profile.ArtifactsDirectory, $"{ts}-{profile.DatabaseName}-receipt.json");
        await File.WriteAllTextAsync(receiptPath,
            JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
        writer.Info($"Receipt: {receiptPath}");
        return ExitCodes.Success;
    }

    private static async Task RunHookAsync(
        string? command,
        HookPhase phase,
        ResolvedProfile profile,
        ChangeSet changeSet,
        IConsoleWriter writer)
    {
        if (string.IsNullOrWhiteSpace(command)) return;
        try
        {
            var ctx = new HookContext(profile.ProfileName, profile.WorkspaceUrl, profile.DatabaseName, phase,
                ChangeSetSnapshot.From(changeSet));
            var result = await new HookRunner().RunAsync(new HookDefinition(phase, command), ctx);
            if (!string.IsNullOrEmpty(result.Stdout)) writer.Info(result.Stdout);
            if (!string.IsNullOrEmpty(result.Stderr)) writer.Error(result.Stderr);
            if (result.ExitCode != 0)
                writer.Error($"Hook '{phase}' exited {result.ExitCode} (non-fatal).");
        }
        catch (Exception ex)
        {
            writer.Error($"Hook '{phase}' failed: {ex.Message} (continuing).");
        }
    }
}

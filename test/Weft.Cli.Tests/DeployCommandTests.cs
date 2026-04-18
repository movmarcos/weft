// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using NSubstitute;
using Weft.Cli.Commands;
using Weft.Cli.Options;
using Weft.Cli.Tests.Helpers;
using Weft.Core.Abstractions;
using Weft.Core.Loading;
using Weft.Xmla;

namespace Weft.Cli.Tests;

public class DeployCommandTests
{
    private static string TinyStaticPath() =>
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "test", "Weft.Core.Tests", "fixtures", "models", "tiny-static.bim");

    private static ResolvedProfile MakeProfile(string src, string artifactsDir, bool allowDrops = false) =>
        new(
            ProfileName: "test",
            WorkspaceUrl: "powerbi://x",
            DatabaseName: "TinyStatic",
            SourcePath: src,
            ArtifactsDirectory: artifactsDir,
            Auth: new Weft.Auth.AuthOptions(Weft.Auth.AuthMode.Interactive, "t", "c"),
            Refresh: new Weft.Config.RefreshConfigSection("full", 10, 15,
                new Weft.Config.IncrementalPolicyConfig(true, true, "preserve"),
                new Weft.Config.DynamicPartitionStrategyConfig("newestOnly", 1)),
            AllowDrops: allowDrops,
            AllowHistoryLoss: false,
            NoRefresh: false,
            ResetBookmarks: false,
            EffectiveDate: null,
            ParameterValues: new Dictionary<string, object?>(),
            ParameterCliOverrides: null,
            ParameterDeclarations: Array.Empty<Weft.Core.Parameters.ParameterDeclaration>(),
            Hooks: new Weft.Config.HooksConfigSection(null, null, null, null, null, null),
            TimeoutMinutes: 60);

    [Fact]
    public async Task Happy_path_returns_zero_and_writes_pre_post_manifests_and_receipt()
    {
        var src = TinyStaticPath();
        var tgtDb = new BimFileLoader().Load(src); // identical model → empty changeset
        var artifacts = Directory.CreateTempSubdirectory().FullName;

        try
        {
            var exit = await DeployCommand.RunAsync(
                MakeProfile(src, artifacts),
                auth: CliTestHost.MakeAuth(),
                targetReader: CliTestHost.StubTarget(tgtDb),
                executor: CliTestHost.MakeExecutor(),
                refreshRunner: CliTestHost.MakeRefreshRunner(),
                manifestStore: new FilePartitionManifestStore());

            exit.Should().Be(ExitCodes.Success);
            Directory.GetFiles(artifacts, "*-pre-partitions.json").Should().NotBeEmpty();
            Directory.GetFiles(artifacts, "*-post-partitions.json").Should().NotBeEmpty();
            Directory.GetFiles(artifacts, "*-receipt.json").Should().NotBeEmpty();
        }
        finally { Directory.Delete(artifacts, recursive: true); }
    }

    [Fact]
    public async Task Returns_AuthError_when_token_acquisition_throws()
    {
        var src = TinyStaticPath();
        var tgtDb = new BimFileLoader().Load(src);
        var artifacts = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var auth = Substitute.For<IAuthProvider>();
            auth.GetTokenAsync(default).ReturnsForAnyArgs<Task<AccessToken>>(
                _ => throw new InvalidOperationException("aad down"));

            var exit = await DeployCommand.RunAsync(
                MakeProfile(src, artifacts),
                auth: auth,
                targetReader: CliTestHost.StubTarget(tgtDb),
                executor: CliTestHost.MakeExecutor(),
                refreshRunner: CliTestHost.MakeRefreshRunner(),
                manifestStore: new FilePartitionManifestStore());

            exit.Should().Be(ExitCodes.AuthError);
        }
        finally { Directory.Delete(artifacts, recursive: true); }
    }

    [Fact]
    public async Task Returns_DiffValidationError_when_drop_attempted_without_allow_drops()
    {
        var src = TinyStaticPath();
        var tgtDb = new BimFileLoader().Load(src);
        tgtDb.Model.Tables.Add(new Microsoft.AnalysisServices.Tabular.Table { Name = "Orphan" });
        var artifacts = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var exit = await DeployCommand.RunAsync(
                MakeProfile(src, artifacts, allowDrops: false),
                auth: CliTestHost.MakeAuth(),
                targetReader: CliTestHost.StubTarget(tgtDb),
                executor: CliTestHost.MakeExecutor(),
                refreshRunner: CliTestHost.MakeRefreshRunner(),
                manifestStore: new FilePartitionManifestStore());

            exit.Should().Be(ExitCodes.DiffValidationError);
        }
        finally { Directory.Delete(artifacts, recursive: true); }
    }

    [Fact]
    public async Task Returns_TmslExecutionError_when_executor_reports_failure()
    {
        var src = TinyStaticPath();
        var tgtDb = new BimFileLoader().Load(src);
        tgtDb.Model.Tables["FactSales"].Columns.Add(
            new Microsoft.AnalysisServices.Tabular.DataColumn
            {
                Name = "ToDelete", DataType = Microsoft.AnalysisServices.Tabular.DataType.String, SourceColumn = "x"
            });
        var artifacts = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var exit = await DeployCommand.RunAsync(
                MakeProfile(src, artifacts),
                auth: CliTestHost.MakeAuth(),
                targetReader: CliTestHost.StubTarget(tgtDb),
                executor: CliTestHost.MakeExecutor(success: false),
                refreshRunner: CliTestHost.MakeRefreshRunner(),
                manifestStore: new FilePartitionManifestStore());

            exit.Should().Be(ExitCodes.TmslExecutionError);
        }
        finally { Directory.Delete(artifacts, recursive: true); }
    }
}

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
    public void Source_workspace_database_are_optional_when_config_target_supplied()
    {
        // Regression: deploy used to fail with `--source is required` at parse time even when
        // weft.yaml supplied source.path. The CLI's documented YAML-fallback was unreachable.
        var cmd = DeployCommand.Build();
        var parse = cmd.Parse(new[] { "--config", "anything.yaml", "--target", "dev" });
        parse.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Deploy_still_fails_clearly_when_neither_source_flag_nor_yaml_provides_it()
    {
        // Same parse layer accepts the args (no --source), but the action handler should fail
        // with a clean ConfigError + a message that names both options the user could use.
        // We can't easily exercise that here without a real subprocess; covered by smoke tests.
        // This test just locks in that the parse layer accepts what we want it to.
        var cmd = DeployCommand.Build();
        var parse = cmd.Parse(new[] { "--target", "dev" });
        parse.Errors.Should().BeEmpty();
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

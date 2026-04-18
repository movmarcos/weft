// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Weft.Auth;
using Weft.Cli;
using Weft.Cli.Commands;
using Weft.Cli.Options;
using Weft.Xmla;

namespace Weft.Integration.Tests;

public class EndToEndDeployTests
{
    [IntegrationTestFact]
    public async Task Deploys_tiny_static_against_test_workspace()
    {
        var workspace = Environment.GetEnvironmentVariable("WEFT_INT_WORKSPACE")!;
        var database  = Environment.GetEnvironmentVariable("WEFT_INT_DATABASE")!;
        var tenant    = Environment.GetEnvironmentVariable("WEFT_INT_TENANT_ID")!;
        var clientId  = Environment.GetEnvironmentVariable("WEFT_INT_CLIENT_ID")!;
        var secret    = Environment.GetEnvironmentVariable("WEFT_INT_CLIENT_SECRET")!;

        var fixture = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "test", "Weft.Core.Tests", "fixtures", "models", "tiny-static.bim");

        var artifacts = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var authOptions = new AuthOptions(
                AuthMode.ServicePrincipalSecret, tenant, clientId, ClientSecret: secret);
            var auth = AuthProviderFactory.Create(authOptions);

            var profile = new ResolvedProfile(
                ProfileName: "integration",
                WorkspaceUrl: workspace,
                DatabaseName: database,
                SourcePath: fixture,
                ArtifactsDirectory: artifacts,
                Auth: authOptions,
                Refresh: new Weft.Config.RefreshConfigSection("full", 10, 15,
                    new Weft.Config.IncrementalPolicyConfig(true, true, "preserve"),
                    new Weft.Config.DynamicPartitionStrategyConfig("newestOnly", 1)),
                AllowDrops: true,
                AllowHistoryLoss: false,
                NoRefresh: true,
                ResetBookmarks: false,
                EffectiveDate: null,
                ParameterValues: new Dictionary<string, object?>(),
                ParameterCliOverrides: null,
                ParameterDeclarations: Array.Empty<Weft.Core.Parameters.ParameterDeclaration>(),
                Hooks: new Weft.Config.HooksConfigSection(null, null, null, null, null, null),
                TimeoutMinutes: 60);

            var exit = await DeployCommand.RunAsync(
                profile,
                auth: auth,
                targetReader: new TargetReader(),
                executor: new XmlaExecutor(),
                refreshRunner: new RefreshRunner(new XmlaExecutor()),
                manifestStore: new FilePartitionManifestStore());

            exit.Should().Be(ExitCodes.Success);
            Directory.GetFiles(artifacts, "*-pre-partitions.json").Should().NotBeEmpty();
            Directory.GetFiles(artifacts, "*-post-partitions.json").Should().NotBeEmpty();
            Directory.GetFiles(artifacts, "*-receipt.json").Should().NotBeEmpty();
        }
        finally { Directory.Delete(artifacts, recursive: true); }
    }
}

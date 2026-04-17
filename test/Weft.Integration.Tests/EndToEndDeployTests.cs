// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Weft.Auth;
using Weft.Cli;
using Weft.Cli.Commands;
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
            var auth = AuthProviderFactory.Create(new AuthOptions(
                AuthMode.ServicePrincipalSecret, tenant, clientId, ClientSecret: secret));

            var exit = await DeployCommand.RunAsync(
                source: fixture,
                workspaceUrl: workspace,
                databaseName: database,
                artifactsDirectory: artifacts,
                allowDrops: true,
                noRefresh: true,
                resetBookmarks: false,
                effectiveDate: null,
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

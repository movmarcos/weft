// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Weft.Cli.Commands;
using Weft.Cli.Options;
using Weft.Cli.Tests.Helpers;
using Weft.Config;
using Weft.Core.Loading;
using Weft.Xmla;
using Xunit;

namespace Weft.Cli.Tests;

public class DeployCommandConfigTests
{
    [Fact]
    public async Task Deploys_using_config_file_and_target_profile()
    {
        var fixturesRoot = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "test", "Weft.Core.Tests", "fixtures", "models");
        var yamlPath = Path.Combine(AppContext.BaseDirectory, "fixtures", "weft-e2e.yaml");
        var bimPath = Path.Combine(fixturesRoot, "tiny-static.bim");

        var config = YamlConfigLoader.LoadFromFile(yamlPath);
        var artifacts = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var profile = ProfileResolver.Build(
                config: config,
                profileName: "test",
                sourcePath: bimPath,
                artifactsDirectory: artifacts,
                noRefresh: true,
                resetBookmarks: false,
                effectiveDate: null,
                cliParameters: null);

            var tgtDb = new BimFileLoader().Load(bimPath);

            var exit = await DeployCommand.RunAsync(
                profile,
                auth: CliTestHost.MakeAuth(),
                targetReader: CliTestHost.StubTarget(tgtDb),
                executor: CliTestHost.MakeExecutor(),
                refreshRunner: CliTestHost.MakeRefreshRunner(),
                manifestStore: new FilePartitionManifestStore());

            exit.Should().Be(ExitCodes.Success);
            Directory.GetFiles(artifacts, "*-receipt.json").Should().NotBeEmpty();
        }
        finally { Directory.Delete(artifacts, recursive: true); }
    }
}

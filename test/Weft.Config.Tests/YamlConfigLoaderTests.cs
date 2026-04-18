// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Weft.Config;

namespace Weft.Config.Tests;

public class YamlConfigLoaderTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [Fact]
    public void Loads_full_weft_yaml()
    {
        var cfg = YamlConfigLoader.LoadFromFile(FixturePath("weft.yaml"));

        cfg.Version.Should().Be(1);
        cfg.Source!.Format.Should().Be("bim");
        cfg.Defaults!.Refresh!.IncrementalPolicy!.BookmarkMode.Should().Be("preserve");
        cfg.Profiles.Should().ContainKeys("dev", "prod");
        cfg.Profiles["prod"].Auth.Mode.Should().Be("ServicePrincipalCertStore");
        cfg.Profiles["prod"].Parameters.Should().ContainKey("DatabaseName")
            .WhoseValue.Should().Be("EDW_PROD");
        cfg.Parameters.Should().ContainSingle(p => p.Name == "DatabaseName" && p.Required);
        cfg.Hooks!.PreDeploy.Should().Be("./hooks/notify-teams.ps1");
    }

    [Fact]
    public void Throws_on_missing_file()
    {
        var act = () => YamlConfigLoader.LoadFromFile("/no/such/weft.yaml");
        act.Should().Throw<FileNotFoundException>();
    }
}

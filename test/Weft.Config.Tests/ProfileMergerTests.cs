// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Weft.Config;

namespace Weft.Config.Tests;

public class ProfileMergerTests
{
    [Fact]
    public void Profile_allowDrops_overrides_defaults_when_set()
    {
        var cfg = YamlConfigLoader.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "weft.yaml"));

        var eff = new ProfileMerger().Merge(cfg, "prod");

        eff.AllowDrops.Should().BeFalse();
        eff.AllowHistoryLoss.Should().BeFalse();
    }

    [Fact]
    public void Profile_inherits_defaults_refresh_policy_when_unset()
    {
        var cfg = YamlConfigLoader.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "weft.yaml"));

        var eff = new ProfileMerger().Merge(cfg, "dev");

        eff.Refresh!.IncrementalPolicy!.BookmarkMode.Should().Be("preserve");
        eff.Refresh.MaxParallelism.Should().Be(10);
    }

    [Fact]
    public void Throws_on_unknown_profile()
    {
        var cfg = YamlConfigLoader.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "weft.yaml"));
        var act = () => new ProfileMerger().Merge(cfg, "ghost");
        act.Should().Throw<WeftConfigValidationException>().WithMessage("*ghost*");
    }
}

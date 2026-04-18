// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using WeftStudio.App.AppSettings;

namespace WeftStudio.App.Tests;

public class ClientIdProviderTests
{
    [Fact]
    public void Commandline_arg_takes_highest_precedence()
    {
        var id = ClientIdProvider.Resolve(
            commandLineArg: "cli-guid",
            envVar: "env-guid",
            userOverride: "settings-guid",
            baked: "baked-guid");
        id.Should().Be("cli-guid");
    }

    [Fact]
    public void Env_var_second_when_no_commandline()
    {
        var id = ClientIdProvider.Resolve(
            commandLineArg: null,
            envVar: "env-guid",
            userOverride: "settings-guid",
            baked: "baked-guid");
        id.Should().Be("env-guid");
    }

    [Fact]
    public void Settings_override_third_when_no_commandline_or_env()
    {
        var id = ClientIdProvider.Resolve(null, null, "settings-guid", "baked-guid");
        id.Should().Be("settings-guid");
    }

    [Fact]
    public void Baked_default_fallback_when_nothing_else_set()
    {
        var id = ClientIdProvider.Resolve(null, null, null, "baked-guid");
        id.Should().Be("baked-guid");
    }

    [Fact]
    public void Empty_strings_are_treated_as_null()
    {
        var id = ClientIdProvider.Resolve("", "", "", "baked-guid");
        id.Should().Be("baked-guid");
    }

    [Fact]
    public void All_empty_returns_empty_string_not_null()
    {
        var id = ClientIdProvider.Resolve(null, null, null, "");
        id.Should().Be("");
    }
}

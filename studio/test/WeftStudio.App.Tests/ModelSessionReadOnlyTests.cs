// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;

namespace WeftStudio.App.Tests;

public class ModelSessionReadOnlyTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim");

    [Fact]
    public void OpenBim_sessions_are_not_ReadOnly()
    {
        var s = ModelSession.OpenBim(FixturePath);
        s.ReadOnly.Should().BeFalse();
    }

    [Fact]
    public void Workspace_ctor_produces_ReadOnly_session()
    {
        var source = ModelSession.OpenBim(FixturePath);
        var ws = new ModelSession(source.Database, sourcePath: null, readOnly: true);
        ws.ReadOnly.Should().BeTrue();
        ws.SourcePath.Should().BeNull();
    }
}

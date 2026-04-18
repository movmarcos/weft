// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core;
using Weft.Core.Tests.Fixtures;
using Xunit;

namespace Weft.Core.Tests;

public class WeftCoreTests
{
    [Fact]
    public void Plan_returns_changeset_and_tmsl_for_in_memory_databases()
    {
        var src = FixtureLoader.LoadBim("models/tiny-static.bim");
        var tgt = FixtureLoader.LoadBim("models/tiny-static.bim");
        src.Model.Tables.Add(new Table { Name = "NewTable" });

        var result = WeftCore.Plan(src, tgt);

        result.ChangeSet.TablesToAdd.Should().ContainSingle().Which.Name.Should().Be("NewTable");
        result.TmslJson.Should().Contain("\"create\"").And.Contain("NewTable");
        result.TmslJson.Should().Contain("\"sequence\"");
    }
}

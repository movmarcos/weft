// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Weft.Core.Loading;
using WeftStudio.App;

namespace WeftStudio.App.Tests;

public class ModelSessionTests
{
    [Fact]
    public void OpenBim_loads_database_and_exposes_model_name()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "simple.bim");

        var session = ModelSession.OpenBim(path);

        session.Database.Should().NotBeNull();
        session.Database.Model.Should().NotBeNull();
        session.SourcePath.Should().Be(path);
        session.IsDirty.Should().BeFalse();
    }
}

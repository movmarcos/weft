// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Xunit;

namespace Weft.Core.Tests.Sanity;

public class SanityTests
{
    [Fact]
    public void Tom_assembly_loads()
    {
        var dbType = typeof(Microsoft.AnalysisServices.Tabular.Database);
        dbType.Should().NotBeNull();
    }
}

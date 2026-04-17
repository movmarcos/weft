// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.RefreshPolicy;
using Xunit;

namespace Weft.Core.Tests.RefreshPolicy;

public class RefreshPolicyComparerTests
{
    private static BasicRefreshPolicy MakePolicy() => new()
    {
        RollingWindowGranularity = RefreshGranularityType.Year,
        RollingWindowPeriods = 5,
        IncrementalGranularity = RefreshGranularityType.Day,
        IncrementalPeriods = 10,
        IncrementalPeriodsOffset = 0,
        SourceExpression = "let Source = ... in Source",
        PollingExpression = null
    };

    [Fact]
    public void Equal_when_all_fields_match() =>
        new RefreshPolicyComparer().AreEqual(MakePolicy(), MakePolicy()).Should().BeTrue();

    [Fact]
    public void Not_equal_when_RollingWindowPeriods_differ()
    {
        var a = MakePolicy(); var b = MakePolicy(); b.RollingWindowPeriods = 3;
        new RefreshPolicyComparer().AreEqual(a, b).Should().BeFalse();
    }

    [Fact]
    public void Not_equal_when_one_side_null()
    {
        new RefreshPolicyComparer().AreEqual(MakePolicy(), null).Should().BeFalse();
        new RefreshPolicyComparer().AreEqual(null, MakePolicy()).Should().BeFalse();
    }

    [Fact]
    public void Equal_when_both_null() =>
        new RefreshPolicyComparer().AreEqual(null, null).Should().BeTrue();
}

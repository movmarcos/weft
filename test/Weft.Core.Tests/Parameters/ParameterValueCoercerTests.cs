// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Weft.Core.Parameters;
using Xunit;

namespace Weft.Core.Tests.Parameters;

public class ParameterValueCoercerTests
{
    [Fact]
    public void String_value_becomes_quoted_M_literal() =>
        ParameterValueCoercer.ToMLiteral("string", "EDW").Should().Be("\"EDW\"");

    [Fact]
    public void Bool_true_becomes_M_true_literal() =>
        ParameterValueCoercer.ToMLiteral("bool", true).Should().Be("true");

    [Fact]
    public void Bool_string_true_becomes_M_true_literal() =>
        ParameterValueCoercer.ToMLiteral("bool", "true").Should().Be("true");

    [Fact]
    public void Int_value_becomes_bare_number() =>
        ParameterValueCoercer.ToMLiteral("int", 42).Should().Be("42");

    [Fact]
    public void Type_mismatch_throws()
    {
        var act = () => ParameterValueCoercer.ToMLiteral("int", "not-a-number");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Escapes_quote_in_string_value() =>
        ParameterValueCoercer.ToMLiteral("string", "a\"b").Should().Be("\"a\"\"b\"");
}

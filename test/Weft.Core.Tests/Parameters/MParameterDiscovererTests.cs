// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Parameters;
using Xunit;

namespace Weft.Core.Tests.Parameters;

public class MParameterDiscovererTests
{
    [Fact]
    public void Returns_empty_when_model_has_no_expressions()
    {
        var db = new Database { Name = "D", CompatibilityLevel = 1600 };
        db.Model = new Model();
        new MParameterDiscoverer().Discover(db).Should().BeEmpty();
    }

    [Fact]
    public void Finds_parameter_expressions_by_IsParameterQuery_annotation()
    {
        var db = new Database { Name = "D", CompatibilityLevel = 1600 };
        db.Model = new Model();

        var pExpr = new NamedExpression
        {
            Name = "DatabaseName",
            Kind = ExpressionKind.M,
            Expression = "\"EDW\" meta [IsParameterQuery=true, Type=\"Text\"]"
        };
        pExpr.Annotations.Add(new Annotation { Name = "IsParameterQuery", Value = "true" });
        db.Model.Expressions.Add(pExpr);

        var nonParam = new NamedExpression
        {
            Name = "NotAParam",
            Kind = ExpressionKind.M,
            Expression = "let x = 1 in x"
        };
        db.Model.Expressions.Add(nonParam);

        var found = new MParameterDiscoverer().Discover(db);
        found.Select(p => p.Name).Should().Equal("DatabaseName");
        found.Single().ExpressionText.Should().StartWith("\"EDW\"");
    }
}

// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Parameters;
using Xunit;

namespace Weft.Core.Tests.Parameters;

public class ParameterResolverTests
{
    private static Database MakeDbWithParam(string name, string initialLiteral)
    {
        var db = new Database { Name = "D", CompatibilityLevel = 1600 };
        db.Model = new Model();
        var e = new NamedExpression
        {
            Name = name,
            Kind = ExpressionKind.M,
            Expression = initialLiteral
        };
        e.Annotations.Add(new Annotation { Name = "IsParameterQuery", Value = "true" });
        db.Model.Expressions.Add(e);
        return db;
    }

    [Fact]
    public void Resolution_priority_cli_beats_profile()
    {
        var db = MakeDbWithParam("DatabaseName", "\"EDW\"");
        var resolver = new ParameterResolver();

        var resolutions = resolver.Resolve(
            sourceDb: db,
            declarations: new[]
            {
                new ParameterDeclaration("DatabaseName", null, "string", true, null)
            },
            profileValues: new Dictionary<string, object?> { ["DatabaseName"] = "EDW_YAML" },
            cliOverrides: new Dictionary<string, string> { ["DatabaseName"] = "EDW_CLI" },
            paramsFileValues: null);

        resolutions.Single().RawValue.Should().Be("EDW_CLI");
        resolutions.Single().Source.Should().Be(ParameterValueSource.Cli);
    }

    [Fact]
    public void Required_parameter_without_value_throws()
    {
        var db = MakeDbWithParam("DatabaseName", "\"EDW\"");
        var resolver = new ParameterResolver();
        var act = () => resolver.Resolve(
            sourceDb: db,
            declarations: new[]
            {
                new ParameterDeclaration("DatabaseName", null, "string", true, null)
            },
            profileValues: new Dictionary<string, object?>(),
            cliOverrides: null,
            paramsFileValues: null);
        act.Should().Throw<ParameterApplicationException>().WithMessage("*DatabaseName*");
    }

    [Fact]
    public void Apply_rewrites_parameter_expression_in_place()
    {
        var db = MakeDbWithParam("DatabaseName", "\"EDW\"");
        var resolver = new ParameterResolver();

        var resolutions = resolver.Resolve(
            sourceDb: db,
            declarations: new[]
            {
                new ParameterDeclaration("DatabaseName", null, "string", true, null)
            },
            profileValues: new Dictionary<string, object?> { ["DatabaseName"] = "EDW_PROD" },
            cliOverrides: null,
            paramsFileValues: null);

        resolver.Apply(db, resolutions);

        db.Model.Expressions["DatabaseName"].Expression.Should().Be("\"EDW_PROD\"");
    }
}

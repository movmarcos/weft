// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AnalysisServices.Tabular;

namespace Weft.Core.Parameters;

public sealed record DiscoveredMParameter(string Name, string ExpressionText, NamedExpression Source);

public sealed class MParameterDiscoverer
{
    public IReadOnlyList<DiscoveredMParameter> Discover(Database database)
    {
        var result = new List<DiscoveredMParameter>();
        foreach (var expr in database.Model.Expressions)
        {
            if (expr.Kind != ExpressionKind.M) continue;
            if (!IsParameterQuery(expr)) continue;
            result.Add(new DiscoveredMParameter(expr.Name, expr.Expression, expr));
        }
        return result;
    }

    private static bool IsParameterQuery(NamedExpression expr)
    {
        var annotation = expr.Annotations.Find("IsParameterQuery");
        return string.Equals(annotation?.Value, "true", StringComparison.OrdinalIgnoreCase);
    }
}

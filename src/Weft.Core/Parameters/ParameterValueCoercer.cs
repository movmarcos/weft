// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;

namespace Weft.Core.Parameters;

public static class ParameterValueCoercer
{
    public static string ToMLiteral(string declaredType, object? rawValue)
    {
        switch (declaredType.ToLowerInvariant())
        {
            case "string":
            {
                var s = rawValue?.ToString() ?? "";
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            }
            case "bool":
            {
                var b = rawValue switch
                {
                    bool x => x,
                    string s => bool.Parse(s),
                    _ => throw new FormatException($"Cannot coerce '{rawValue}' to bool.")
                };
                return b ? "true" : "false";
            }
            case "int":
            {
                var i = rawValue switch
                {
                    int x => x,
                    long x => checked((int)x),
                    string s => int.Parse(s, CultureInfo.InvariantCulture),
                    _ => throw new FormatException($"Cannot coerce '{rawValue}' to int.")
                };
                return i.ToString(CultureInfo.InvariantCulture);
            }
            default:
                throw new NotSupportedException($"Unsupported declared type: '{declaredType}'.");
        }
    }
}

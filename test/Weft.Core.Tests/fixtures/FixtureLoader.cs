// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using Microsoft.AnalysisServices.Tabular;

namespace Weft.Core.Tests.Fixtures;

public static class FixtureLoader
{
    public static string FixturePath(params string[] segments)
        => Path.Combine(new[] { AppContext.BaseDirectory, "fixtures" }.Concat(segments).ToArray());

    public static Database LoadBim(string relativePath)
    {
        var fullPath = Path.Combine(AppContext.BaseDirectory, "fixtures", relativePath);
        var json = File.ReadAllText(fullPath);
        return JsonSerializer.DeserializeDatabase(json);
    }
}

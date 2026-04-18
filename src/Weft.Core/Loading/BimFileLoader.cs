// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using Microsoft.AnalysisServices.Tabular;

namespace Weft.Core.Loading;

public sealed class BimFileLoader : IModelLoader
{
    public Database Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Source .bim not found: {path}", path);

        var json = File.ReadAllText(path);
        return JsonSerializer.DeserializeDatabase(json);
    }
}

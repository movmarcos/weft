// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AnalysisServices.Tabular;
using TomJsonSerializer = Microsoft.AnalysisServices.Tabular.JsonSerializer;

namespace Weft.Core.Loading;

public sealed class TabularEditorFolderLoader : IModelLoader
{
    public Database Load(string path)
    {
        var dbPath = Path.Combine(path, "database.json");
        if (!File.Exists(dbPath))
            throw new FileNotFoundException($"database.json not found in {path}", dbPath);

        var root = JsonNode.Parse(File.ReadAllText(dbPath))!.AsObject();
        var model = root["model"]!.AsObject();
        var tables = (model["tables"] as JsonArray) ?? new JsonArray();

        var tablesDir = Path.Combine(path, "tables");
        if (Directory.Exists(tablesDir))
        {
            foreach (var file in Directory.EnumerateFiles(tablesDir, "*.json", SearchOption.AllDirectories))
            {
                var tableNode = JsonNode.Parse(File.ReadAllText(file))!;
                tables.Add(tableNode.DeepClone());
            }
        }
        model["tables"] = tables;

        return TomJsonSerializer.DeserializeDatabase(root.ToJsonString());
    }
}

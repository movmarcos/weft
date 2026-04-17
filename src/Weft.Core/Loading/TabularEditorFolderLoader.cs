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

        // Build a fresh tables array so we never attempt to reparent an already-owned JsonNode.
        var tables = new JsonArray();

        // Preserve any tables already declared inline in database.json (rare, but possible).
        var existingTables = model["tables"] as JsonArray;
        if (existingTables is not null)
        {
            foreach (var t in existingTables)
                tables.Add(t!.DeepClone());
        }

        var tablesDir = Path.Combine(path, "tables");
        if (Directory.Exists(tablesDir))
        {
            var seenTableNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in Directory.EnumerateFiles(tablesDir, "*.json", SearchOption.TopDirectoryOnly)
                                          .OrderBy(p => p, StringComparer.Ordinal))
            {
                // Skip macOS Finder duplicates: "DimDate 2.json", "FactSales 3.json", etc.
                var name = Path.GetFileNameWithoutExtension(file);
                if (System.Text.RegularExpressions.Regex.IsMatch(name, @" \d+$"))
                    continue;

                var tableNode = JsonNode.Parse(File.ReadAllText(file))!;
                var tableName = tableNode["name"]?.GetValue<string>();
                if (tableName is not null && !seenTableNames.Add(tableName))
                    continue;  // Defensive: silently skip duplicate-name table files
                tables.Add(tableNode.DeepClone());
            }
        }
        model["tables"] = tables;

        return TomJsonSerializer.DeserializeDatabase(root.ToJsonString());
    }
}

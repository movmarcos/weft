// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Partitions;

namespace Weft.Xmla;

public sealed class BookmarkClearer
{
    public string BuildTmsl(Database target, IEnumerable<string> tableNames)
    {
        var operations = new JsonArray();
        var dbName = target.Name;

        foreach (var tableName in tableNames)
        {
            if (!target.Model.Tables.ContainsName(tableName)) continue;
            var t = target.Model.Tables[tableName];

            foreach (var partition in t.Partitions)
            {
                var bookmark = partition.Annotations.Find(PartitionAnnotationNames.RefreshBookmark);
                if (bookmark is null) continue;

                operations.Add(new JsonObject
                {
                    ["delete"] = new JsonObject
                    {
                        ["object"] = new JsonObject
                        {
                            ["database"]  = dbName,
                            ["table"]     = tableName,
                            ["partition"] = partition.Name,
                            ["annotation"] = PartitionAnnotationNames.RefreshBookmark
                        }
                    }
                });
            }
        }

        var root = new JsonObject
        {
            ["sequence"] = new JsonObject
            {
                ["maxParallelism"] = 1,
                ["operations"] = operations
            }
        };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}

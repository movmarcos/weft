// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Weft.Xmla;

public sealed record RefreshTableEntry(string TableName, RefreshTypeSpec Spec);

public sealed class RefreshCommandBuilder
{
    public string Build(string databaseName, IEnumerable<RefreshTableEntry> entries, string? effectiveDateUtc)
    {
        var operations = new JsonArray();
        foreach (var e in entries)
        {
            var refresh = new JsonObject
            {
                ["type"] = "full",
                ["objects"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["database"] = databaseName,
                        ["table"]    = e.TableName
                    }
                }
            };
            if (e.Spec.ApplyRefreshPolicy)
            {
                refresh["applyRefreshPolicy"] = true;
                if (effectiveDateUtc is not null)
                    refresh["effectiveDate"] = effectiveDateUtc;
            }
            operations.Add(new JsonObject { ["refresh"] = refresh });
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

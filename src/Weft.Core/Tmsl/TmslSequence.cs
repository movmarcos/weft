// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Weft.Core.Tmsl;

public sealed class TmslSequence
{
    private readonly JsonArray _operations = new();

    public void Add(JsonNode op) => _operations.Add(op);

    public string ToJson()
    {
        var root = new JsonObject
        {
            ["sequence"] = new JsonObject
            {
                ["maxParallelism"] = 1,
                ["operations"] = _operations
            }
        };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}

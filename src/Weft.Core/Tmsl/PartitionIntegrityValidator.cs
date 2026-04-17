// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Diffing;
using Weft.Core.Partitions;

namespace Weft.Core.Tmsl;

public sealed class PartitionIntegrityValidator
{
    public void Validate(string tmslJson, Database target, ChangeSet changeSet)
    {
        var root = JsonNode.Parse(tmslJson)!.AsObject();
        var operations = root["sequence"]?["operations"]?.AsArray();
        if (operations is null) return;

        var droppedTables = new HashSet<string>(changeSet.TablesToDrop, StringComparer.Ordinal);

        foreach (var op in operations)
        {
            if (op?["createOrReplace"] is not JsonObject cor) continue;
            var tableName = cor["object"]?["table"]?.GetValue<string>();
            if (tableName is null) continue;
            if (droppedTables.Contains(tableName)) continue;
            if (!target.Model.Tables.ContainsName(tableName)) continue;

            var existingPartitions = target.Model.Tables[tableName]
                .Partitions.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

            var emittedPartitionNodes = (cor["table"]?["partitions"] as JsonArray) ?? new JsonArray();
            var emittedPartitionNames = emittedPartitionNodes
                .Select(p => p?["name"]?.GetValue<string>())
                .Where(n => n is not null)
                .Cast<string>()
                .ToHashSet(StringComparer.Ordinal);

            var missing = existingPartitions.Except(emittedPartitionNames).OrderBy(x => x).ToList();
            if (missing.Count > 0)
            {
                throw new PartitionIntegrityException(
                    $"Partition integrity violation on table '{tableName}': " +
                    $"the generated TMSL would remove existing partition(s) {string.Join(", ", missing)}. " +
                    $"This is forbidden for preserved tables (see spec §5.4).");
            }

            // Bookmark preservation: every emitted partition that exists on target must
            // carry the same RefreshBookmark annotation (or both must be empty).
            foreach (var emitted in emittedPartitionNodes)
            {
                var name = emitted?["name"]?.GetValue<string>();
                if (name is null) continue;
                if (!target.Model.Tables[tableName].Partitions.ContainsName(name)) continue;

                var targetPartition = target.Model.Tables[tableName].Partitions[name];
                var targetBookmark = targetPartition.Annotations
                    .Find(PartitionAnnotationNames.RefreshBookmark)?.Value;
                if (string.IsNullOrEmpty(targetBookmark)) continue;

                var emittedBookmark = ((emitted!["annotations"] as JsonArray) ?? new JsonArray())
                    .OfType<JsonObject>()
                    .FirstOrDefault(a => a["name"]?.GetValue<string>() == PartitionAnnotationNames.RefreshBookmark)
                    ?["value"]?.GetValue<string>();

                if (!string.Equals(targetBookmark, emittedBookmark, StringComparison.Ordinal))
                {
                    throw new PartitionIntegrityException(
                        $"Bookmark integrity violation on '{tableName}'/'{name}': " +
                        $"target RefreshBookmark '{targetBookmark}' was not preserved in generated TMSL " +
                        $"(emitted: '{emittedBookmark ?? "<missing>"}').");
                }
            }
        }
    }
}

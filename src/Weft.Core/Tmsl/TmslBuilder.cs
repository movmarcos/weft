// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Diffing;
using TomJsonSerializer = Microsoft.AnalysisServices.Tabular.JsonSerializer;

namespace Weft.Core.Tmsl;

public sealed class TmslBuilder
{
    public string Build(ChangeSet changeSet, Database source, Database target)
    {
        var seq = new TmslSequence();
        var dbName = target.Name;

        foreach (var name in changeSet.TablesToDrop)
            seq.Add(DeleteTable(dbName, name));

        foreach (var add in changeSet.TablesToAdd)
            seq.Add(CreateTable(dbName, add.SourceTable));

        foreach (var alter in changeSet.TablesToAlter)
            seq.Add(AlterTable(dbName, alter, source, target));

        var json = seq.ToJson();
        new PartitionIntegrityValidator().Validate(json, target, changeSet);
        return json;
    }

    private static JsonNode DeleteTable(string database, string table) =>
        new JsonObject
        {
            ["delete"] = new JsonObject
            {
                ["object"] = new JsonObject
                {
                    ["database"] = database,
                    ["table"] = table
                }
            }
        };

    private static JsonNode AlterTable(string database, TableDiff diff, Database source, Database target)
    {
        var srcTable = source.Model.Tables[diff.Name];
        var tgtTable = target.Model.Tables[diff.Name];

        // Build a new Table that has source schema but TARGET partitions (deep-cloned so
        // every partition-level property — including the RefreshBookmark annotation — carries over).
        var merged = srcTable.Clone();
        merged.Partitions.Clear();
        foreach (var p in tgtTable.Partitions)
            merged.Partitions.Add(p.Clone());

        var tableJson = SerializeTableObject(merged);

        return new JsonObject
        {
            ["createOrReplace"] = new JsonObject
            {
                ["object"] = new JsonObject
                {
                    ["database"] = database,
                    ["table"]    = diff.Name
                },
                ["table"] = tableJson
            }
        };
    }

    private static JsonNode CreateTable(string database, Table table)
    {
        var tableJson = SerializeTableObject(table);
        return new JsonObject
        {
            ["create"] = new JsonObject
            {
                ["parentObject"] = new JsonObject
                {
                    ["database"] = database,
                    ["model"] = new JsonObject()
                },
                ["table"] = tableJson
            }
        };
    }

    internal static JsonNode SerializeTableObject(Table table)
    {
        var clone = new Database { Name = "_t", CompatibilityLevel = 1600 };
        clone.Model = new Model();
        clone.Model.Tables.Add(table.Clone());
        var dbJson = TomJsonSerializer.SerializeDatabase(clone, new SerializeOptions
        {
            IgnoreTimestamps = true,
            IgnoreInferredObjects = true,
            IgnoreInferredProperties = true
        });
        var node = JsonNode.Parse(dbJson)!;
        return node["model"]!["tables"]!.AsArray().First()!.DeepClone();
    }
}

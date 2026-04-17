// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using Weft.Cli.Options;
using Weft.Core.Loading;
using Weft.Core.Partitions;

namespace Weft.Cli.Commands;

public static class InspectCommand
{
    public static Command Build()
    {
        var snap = new Option<string>("--target-snapshot")
            { Description = "Read partitions from a .bim snapshot file.", Required = true };
        var table = new Option<string?>("--table") { Description = "Filter to one table." };

        var partitions = new Command("partitions", "List partitions and bookmarks.");
        partitions.Options.Add(snap); partitions.Options.Add(table);
        partitions.SetAction(async (parse, ct) =>
            await RunFromSnapshotAsync(parse.GetValue(snap)!, parse.GetValue(table)));

        var inspect = new Command("inspect", "Inspect target state.");
        inspect.Subcommands.Add(partitions);
        return inspect;
    }

    public static Task<int> RunFromSnapshotAsync(string snapshotPath, string? tableFilter)
    {
        try
        {
            var db = ModelLoaderFactory.For(snapshotPath).Load(snapshotPath);
            var manifest = new PartitionManifestReader().Read(db);
            foreach (var (tableName, parts) in manifest.Tables)
            {
                if (tableFilter is not null && !string.Equals(tableName, tableFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
                Console.Out.WriteLine($"Table: {tableName}");
                foreach (var p in parts)
                    Console.Out.WriteLine($"  - {p.Name}    bookmark={p.RefreshBookmark ?? "<none>"}");
            }
            return Task.FromResult(ExitCodes.Success);
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"Snapshot not found: {ex.Message}");
            return Task.FromResult(ExitCodes.SourceLoadError);
        }
    }
}

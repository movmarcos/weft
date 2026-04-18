// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using Weft.Cli.Options;
using Weft.Config;
using Weft.Core;
using Weft.Core.Loading;

namespace Weft.Cli.Commands;

public static class PlanCommand
{
    public static Command Build()
    {
        var src = new Option<string?>("--source", "-s") { Description = "Path to source .bim or TE folder." };
        var tgt = new Option<string>("--target-snapshot")
            { Description = "Path to a .bim snapshot of the target (offline plan).", Required = true };
        var artifacts = CommonOptions.ArtifactsOption();
        var configOpt = CommonOptions.ConfigFileOption();
        var targetOpt = CommonOptions.TargetProfileOption();

        var cmd = new Command("plan", "Compute and print a deploy plan; write TMSL to artifacts.");
        cmd.Options.Add(src); cmd.Options.Add(tgt); cmd.Options.Add(artifacts);
        cmd.Options.Add(configOpt); cmd.Options.Add(targetOpt);
        cmd.SetAction(async (parse, ct) =>
        {
            WeftConfig? config = null;
            var configPath = parse.GetValue(configOpt);
            if (!string.IsNullOrEmpty(configPath))
            {
                try { config = YamlConfigLoader.LoadFromFile(configPath); }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Config load failed: {ex.Message}");
                    return ExitCodes.ConfigError;
                }
            }

            var sourcePath = parse.GetValue(src) ?? config?.Source?.Path
                ?? throw new InvalidOperationException("--source required (or configure in weft.yaml).");

            return await RunAsync(sourcePath, parse.GetValue(tgt)!, parse.GetValue(artifacts)!);
        });
        return cmd;
    }

    public static Task<int> RunAsync(string source, string targetSnapshot, string artifactsDirectory)
    {
        try
        {
            var srcDb = ModelLoaderFactory.For(source).Load(source);
            var tgtDb = ModelLoaderFactory.For(targetSnapshot).Load(targetSnapshot);
            var result = WeftCore.Plan(srcDb, tgtDb);

            Directory.CreateDirectory(artifactsDirectory);
            var ts = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
            var planPath = Path.Combine(artifactsDirectory, $"{ts}-{tgtDb.Name}-plan.tmsl");
            File.WriteAllText(planPath, result.TmslJson);

            Console.Out.WriteLine($"Plan written to {planPath}");
            Console.Out.WriteLine($"  Add:       {result.ChangeSet.TablesToAdd.Count}");
            Console.Out.WriteLine($"  Drop:      {result.ChangeSet.TablesToDrop.Count}");
            Console.Out.WriteLine($"  Alter:     {result.ChangeSet.TablesToAlter.Count}");
            Console.Out.WriteLine($"  Unchanged: {result.ChangeSet.TablesUnchanged.Count}");
            return Task.FromResult(ExitCodes.Success);
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"Source/target not found: {ex.Message}");
            return Task.FromResult(ExitCodes.SourceLoadError);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Plan failed: {ex.Message}");
            return Task.FromResult(ExitCodes.DiffValidationError);
        }
    }
}

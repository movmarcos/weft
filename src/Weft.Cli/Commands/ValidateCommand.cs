// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using Weft.Cli.Options;
using Weft.Core.Loading;

namespace Weft.Cli.Commands;

public static class ValidateCommand
{
    public static Command Build()
    {
        var src = CommonOptions.SourceOption();
        var cmd = new Command("validate", "Parse and validate a source model.");
        cmd.Options.Add(src);
        cmd.SetAction(async (parse, ct) =>
        {
            var source = parse.GetValue(src)!;
            return await RunAsync(source);
        });
        return cmd;
    }

    public static Task<int> RunAsync(string sourcePath)
    {
        try
        {
            var loader = ModelLoaderFactory.For(sourcePath);
            var db = loader.Load(sourcePath);
            Console.Out.WriteLine($"OK: '{db.Name}' loaded with {db.Model.Tables.Count} table(s).");
            return Task.FromResult(ExitCodes.Success);
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"Source not found: {ex.Message}");
            return Task.FromResult(ExitCodes.SourceLoadError);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Source load failed: {ex.Message}");
            return Task.FromResult(ExitCodes.SourceLoadError);
        }
    }
}

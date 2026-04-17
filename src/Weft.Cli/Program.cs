// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using Weft.Cli.Commands;

namespace Weft.Cli;

public static class Program
{
    public static Task<int> Main(string[] args) => new CommandLineConfiguration(BuildRoot()).InvokeAsync(args);

    public static RootCommand BuildRoot()
    {
        var root = new RootCommand("Weft — diff-based Power BI semantic-model deploys.");
        root.Subcommands.Add(ValidateCommand.Build());
        root.Subcommands.Add(PlanCommand.Build());
        root.Subcommands.Add(InspectCommand.Build());
        return root;
    }
}

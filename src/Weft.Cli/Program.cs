// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;

namespace Weft.Cli;

public static class Program
{
    public static Task<int> Main(string[] args)
    {
        var root = BuildRoot();
        // System.CommandLine 2.0.0-beta5: InvokeAsync is on CommandLineConfiguration, not on RootCommand.
        return new CommandLineConfiguration(root).InvokeAsync(args);
    }

    public static RootCommand BuildRoot()
    {
        var root = new RootCommand("Weft — diff-based Power BI semantic-model deploys.");
        // Commands wired in subsequent tasks.
        return root;
    }
}

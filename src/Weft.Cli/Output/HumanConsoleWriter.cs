// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Spectre.Console;

namespace Weft.Cli.Output;

// Wired into commands in Plan 3.
public sealed class HumanConsoleWriter : IConsoleWriter
{
    public void Info(string message)  => AnsiConsole.MarkupLine($"[grey]{Markup.Escape(message)}[/]");
    public void Warn(string message)  => AnsiConsole.MarkupLine($"[yellow]WARNING:[/] {Markup.Escape(message)}");
    public void Error(string message) => AnsiConsole.MarkupLine($"[red]ERROR:[/] {Markup.Escape(message)}");

    public void Plan(string headline, IEnumerable<string> bullets)
    {
        AnsiConsole.MarkupLine($"[bold]{Markup.Escape(headline)}[/]");
        foreach (var b in bullets) AnsiConsole.MarkupLine($"  {Markup.Escape(b)}");
    }
}

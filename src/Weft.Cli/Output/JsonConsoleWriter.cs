// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

namespace Weft.Cli.Output;

// Wired into commands in Plan 3.
public sealed class JsonConsoleWriter : IConsoleWriter
{
    public void Info(string message)  => Emit("info", message);
    public void Warn(string message)  => Emit("warn", message);
    public void Error(string message) => Emit("error", message);

    public void Plan(string headline, IEnumerable<string> bullets)
    {
        Console.Out.WriteLine(JsonSerializer.Serialize(new
        {
            ts = DateTimeOffset.UtcNow.ToString("o"),
            level = "plan",
            headline,
            items = bullets.ToArray()
        }));
    }

    private static void Emit(string level, string message) =>
        Console.Out.WriteLine(JsonSerializer.Serialize(new
        {
            ts = DateTimeOffset.UtcNow.ToString("o"),
            level,
            message
        }));
}

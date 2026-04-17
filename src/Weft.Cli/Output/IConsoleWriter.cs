// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace Weft.Cli.Output;

/// <summary>
/// Output formatter abstraction. Currently unused by commands (each writes directly to
/// Console.Out / Console.Error). Plan 3 will wire this through every command alongside
/// the --log-format option.
/// </summary>
public interface IConsoleWriter
{
    void Info(string message);
    void Warn(string message);
    void Error(string message);
    void Plan(string headline, IEnumerable<string> bullets);
}

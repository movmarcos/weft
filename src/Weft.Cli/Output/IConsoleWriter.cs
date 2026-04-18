// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace Weft.Cli.Output;

public interface IConsoleWriter
{
    void Info(string message);
    void Warn(string message);
    void Error(string message);
    void Plan(string headline, IEnumerable<string> bullets);
}

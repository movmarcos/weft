// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using FluentAssertions;

namespace Weft.Cli.Tests;

public class CliSmokeTests
{
    private static string CliPath()
    {
        var rid = "net10.0";
        return Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Weft.Cli", "bin", "Debug", rid, "weft.dll");
    }

    private static (int Exit, string Stdout, string Stderr) Run(params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList = { CliPath() },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, stdout, stderr);
    }

    [Fact]
    public void Help_works()
    {
        var (exit, stdout, _) = Run("--help");
        exit.Should().Be(0);
        stdout.Should().Contain("validate");
        stdout.Should().Contain("plan");
        stdout.Should().Contain("deploy");
        stdout.Should().Contain("refresh");
    }

    [Fact]
    public void Validate_on_fixture_succeeds()
    {
        var fixture = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "test", "Weft.Core.Tests", "fixtures", "models", "tiny-static.bim");
        var (exit, _, _) = Run("validate", "--source", fixture);
        exit.Should().Be(0);
    }
}

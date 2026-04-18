// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;

namespace Weft.Core.Hooks;

public sealed record HookRunResult(int ExitCode, string Stdout, string Stderr);

/// <summary>
/// Spawns the hook executable as a child process with the <see cref="HookContext"/>
/// serialized to JSON on stdin. Child processes inherit sanitized environment —
/// known secret-bearing variables (<c>WEFT_CLIENT_SECRET</c>, <c>WEFT_CERT_PASSWORD</c>,
/// <c>WEFT_CERT_THUMBPRINT</c>, and any <c>WEFT_PARAM_*</c> containing
/// PASSWORD/SECRET/KEY/TOKEN) are removed before <see cref="System.Diagnostics.Process.Start()"/>.
/// </summary>
/// <remarks>
/// The <see cref="HookDefinition.Command"/> string is WHITESPACE-TOKENIZED, not shell-parsed.
/// Quoted arguments are NOT supported. If a hook needs complex arguments (spaces, pipes,
/// redirects), point the command at a shell script and handle parsing there:
/// <code>
/// hooks:
///   preDeploy: ./hooks/notify.sh       # shell script handles its own args
/// </code>
/// </remarks>
public sealed class HookRunner
{
    public async Task<HookRunResult> RunAsync(HookDefinition hook, HookContext context, CancellationToken ct = default)
    {
        var (fileName, args) = SplitCommand(hook.Command);
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment["WEFT_HOOK_PHASE"] = context.Phase.ToString();
        psi.Environment["WEFT_HOOK_PROFILE"] = context.ProfileName;
        psi.Environment["WEFT_HOOK_DATABASE"] = context.DatabaseName;

        // Scrub known secret-bearing env vars from child environment.
        var secretKeys = new[]
        {
            "WEFT_CLIENT_SECRET",
            "WEFT_CERT_PASSWORD",
            "WEFT_CERT_THUMBPRINT"
        };
        foreach (var key in secretKeys)
            psi.Environment.Remove(key);
        // Also scrub any WEFT_PARAM_* that looks secret-like (contains "PASSWORD", "SECRET", "KEY", "TOKEN").
        var paramKeysToScrub = psi.Environment.Keys
            .Where(k => k.StartsWith("WEFT_PARAM_", StringComparison.Ordinal) &&
                        (k.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase) ||
                         k.Contains("SECRET",   StringComparison.OrdinalIgnoreCase) ||
                         k.Contains("KEY",      StringComparison.OrdinalIgnoreCase) ||
                         k.Contains("TOKEN",    StringComparison.OrdinalIgnoreCase)))
            .ToList();
        foreach (var key in paramKeysToScrub)
            psi.Environment.Remove(key);

        using var p = Process.Start(psi)!;
        var json = JsonSerializer.Serialize(context, new JsonSerializerOptions { WriteIndented = false });
        try
        {
            await p.StandardInput.WriteAsync(json);
            p.StandardInput.Close();
        }
        catch (IOException)
        {
            // Process may have exited before reading stdin — not an error.
        }

        var stdoutTask = p.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = p.StandardError.ReadToEndAsync(ct);
        await Task.WhenAll(stdoutTask, stderrTask);
        await p.WaitForExitAsync(ct);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return new HookRunResult(p.ExitCode, stdout, stderr);
    }

    // Whitespace-split tokenizer — see class-level remarks for the rationale.
    private static (string FileName, string[] Args) SplitCommand(string command)
    {
        var tokens = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) throw new ArgumentException("Empty hook command.", nameof(command));
        return (tokens[0], tokens[1..]);
    }
}

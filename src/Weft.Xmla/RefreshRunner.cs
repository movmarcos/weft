// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Weft.Core.Abstractions;
using Weft.Core.Diffing;

namespace Weft.Xmla;

public sealed class RefreshRunner : IRefreshRunner
{
    private readonly IXmlaExecutor _executor;
    private readonly RefreshTypeSelector _selector = new();
    private readonly RefreshCommandBuilder _builder = new();

    public RefreshRunner(IXmlaExecutor executor)
    {
        _executor = executor;
    }

    public async Task<XmlaExecutionResult> RefreshAsync(
        RefreshRequest request,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var entries = new List<RefreshTableEntry>();
        foreach (var add in request.ChangeSet.TablesToAdd)
            entries.Add(new RefreshTableEntry(add.Name, _selector.For(add)));
        foreach (var alt in request.ChangeSet.TablesToAlter)
            entries.Add(new RefreshTableEntry(alt.Name, _selector.For(alt)));

        if (entries.Count == 0)
        {
            progress?.Report("No tables to refresh; nothing changed.");
            return new XmlaExecutionResult(true, new[] { "No refresh required." }, TimeSpan.Zero);
        }

        progress?.Report($"Refreshing {entries.Count} table(s): {string.Join(", ", entries.Select(e => e.TableName))}");
        var tmsl = _builder.Build(request.DatabaseName, entries, request.EffectiveDateUtc);
        var result = await _executor.ExecuteAsync(
            request.WorkspaceUrl, request.DatabaseName, request.Token, tmsl, cancellationToken);

        foreach (var msg in result.Messages)
            progress?.Report(msg);
        return result;
    }
}

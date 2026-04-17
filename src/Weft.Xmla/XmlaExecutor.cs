// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using Microsoft.AnalysisServices.Tabular;
using Weft.Core.Abstractions;

namespace Weft.Xmla;

public sealed class XmlaExecutor : IXmlaExecutor
{
    public Task<XmlaExecutionResult> ExecuteAsync(
        string workspaceUrl,
        string databaseName,
        Weft.Core.Abstractions.AccessToken token,
        string tmslJson,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var messages = new List<string>();

        using var server = new ServerConnectionFactory().Connect(workspaceUrl, databaseName, token);
        Microsoft.AnalysisServices.XmlaResultCollection results;
        try
        {
            results = server.Execute(tmslJson);
        }
        catch (Exception ex)
        {
            messages.Add($"XMLA execution failed: {ex.Message}");
            return Task.FromResult(new XmlaExecutionResult(false, messages, sw.Elapsed));
        }

        var success = true;
        foreach (Microsoft.AnalysisServices.XmlaResult r in results)
        {
            foreach (Microsoft.AnalysisServices.XmlaMessage m in r.Messages)
            {
                messages.Add(m.Description);
                if (m is Microsoft.AnalysisServices.XmlaError) success = false;
            }
        }

        return Task.FromResult(new XmlaExecutionResult(success, messages, sw.Elapsed));
    }
}

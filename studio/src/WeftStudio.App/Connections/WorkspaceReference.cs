// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace WeftStudio.App.Connections;

public sealed record WorkspaceReference(string Server, string WorkspaceName)
{
    private static readonly string[] ValidSchemes = { "powerbi://", "asazure://" };

    public static WorkspaceReference Parse(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new WorkspaceUrlException("XMLA endpoint URL cannot be empty.");

        var trimmed = url.Trim();

        if (!ValidSchemes.Any(s => trimmed.StartsWith(s, StringComparison.OrdinalIgnoreCase)))
            throw new WorkspaceUrlException(
                "Must start with powerbi:// or asazure://");

        // For Power BI Fabric: .../myorg/<workspace-name>
        var workspaceName = "";
        if (trimmed.StartsWith("powerbi://", StringComparison.OrdinalIgnoreCase))
        {
            var marker = "/myorg/";
            var idx = trimmed.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
                workspaceName = trimmed[(idx + marker.Length)..].Trim('/');
        }

        return new WorkspaceReference(trimmed, workspaceName);
    }
}

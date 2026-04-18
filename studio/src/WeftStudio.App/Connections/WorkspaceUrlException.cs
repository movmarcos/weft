// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

namespace WeftStudio.App.Connections;

public sealed class WorkspaceUrlException : Exception
{
    public WorkspaceUrlException(string message) : base(message) { }
}

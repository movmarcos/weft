// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using ReactiveUI;

namespace WeftStudio.Ui.Explorer;

public sealed class TreeNode : ReactiveObject
{
    public TreeNode(string displayName, object? payload = null)
    {
        DisplayName = displayName;
        Payload = payload;
    }
    public string DisplayName { get; }
    public object? Payload { get; }
    public ObservableCollection<TreeNode> Children { get; } = new();
}

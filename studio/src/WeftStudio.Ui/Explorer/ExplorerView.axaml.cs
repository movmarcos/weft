// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;
using Avalonia.Input;
using Microsoft.AnalysisServices.Tabular;

namespace WeftStudio.Ui.Explorer;

public partial class ExplorerView : UserControl
{
    public event Action<string, string>? MeasureDoubleClicked;

    public ExplorerView()
    {
        InitializeComponent();
        var tree = this.FindControl<TreeView>("Tree");
        if (tree is not null) tree.DoubleTapped += OnDouble;
    }

    private void OnDouble(object? sender, TappedEventArgs e)
    {
        if (sender is not TreeView tv) return;
        if (tv.SelectedItem is not TreeNode n || n.Payload is not Measure m) return;
        MeasureDoubleClicked?.Invoke(m.Table.Name, m.Name);
    }
}

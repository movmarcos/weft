// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;
using Avalonia.VisualTree;
using WeftStudio.Ui.Explorer;

namespace WeftStudio.Ui.Shell;

public partial class ShellWindow : Window
{
    public ShellWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var explorerView = this.GetVisualDescendants().OfType<ExplorerView>().FirstOrDefault();
            if (explorerView is null) return;
            explorerView.MeasureDoubleClicked += (table, measure) =>
            {
                if (DataContext is ShellViewModel vm) vm.OpenMeasure(table, measure);
            };
        };
    }
}

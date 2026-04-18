// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace WeftStudio.Ui.Inspector;

public partial class InspectorView : UserControl
{
    public InspectorView() => InitializeComponent();

    private void OnNameLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is InspectorViewModel vm) vm.CommitRename();
    }
}

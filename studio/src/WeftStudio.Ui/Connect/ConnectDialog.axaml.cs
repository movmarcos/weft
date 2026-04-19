// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;
using Avalonia.Interactivity;
using WeftStudio.App;

namespace WeftStudio.Ui.Connect;

public partial class ConnectDialog : Window
{
    public ConnectDialog() => InitializeComponent();

    public ModelSession? Result { get; private set; }
    public string? WorkspaceLabel { get; private set; }

    private async void OnSignIn(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ConnectDialogViewModel vm) await vm.SignInAsync();
    }

    private async void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ConnectDialogViewModel vm) return;
        var session = await vm.OpenAsync();
        if (session is not null)
        {
            Result = session;
            WorkspaceLabel = $"{vm.SelectedRow?.Name}";
            Close();
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}

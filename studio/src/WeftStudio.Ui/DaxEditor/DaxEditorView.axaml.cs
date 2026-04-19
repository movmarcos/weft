// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using System.Xml;
using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace WeftStudio.Ui.DaxEditor;

public partial class DaxEditorView : UserControl
{
    private static IHighlightingDefinition? _daxDef;
    private TextEditor? _editor;

    public DaxEditorView()
    {
        InitializeComponent();
        // Find the named TextEditor right after the XAML has been loaded —
        // OnApplyTemplate is unreliable for UserControls and races against DataContext.
        _editor = this.FindControl<TextEditor>("Editor");
        if (_editor is not null)
        {
            _editor.SyntaxHighlighting = LoadDaxHighlighting();
            _editor.TextChanged += (_, _) =>
            {
                if (DataContext is DaxEditorViewModel vm) vm.Text = _editor.Text;
            };
        }

        DataContextChanged += (_, _) => Sync();
        Sync();
    }

    private void Sync()
    {
        if (_editor is null || DataContext is not DaxEditorViewModel vm) return;
        if (_editor.Text != vm.Text) _editor.Text = vm.Text;
    }

    private static IHighlightingDefinition LoadDaxHighlighting()
    {
        if (_daxDef is not null) return _daxDef;
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("WeftStudio.Ui.DaxEditor.DaxSyntaxHighlighting.xshd")
            ?? throw new InvalidOperationException("DAX xshd not embedded");
        using var reader = XmlReader.Create(stream);
        _daxDef = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        return _daxDef;
    }
}

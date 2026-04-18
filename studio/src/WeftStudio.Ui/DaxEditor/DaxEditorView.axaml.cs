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
    }

    protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _editor = this.FindControl<TextEditor>("Editor");
        if (_editor is null) return;

        _editor.SyntaxHighlighting = LoadDaxHighlighting();

        DataContextChanged += (_, _) => Sync();
        _editor.TextChanged += (_, _) =>
        {
            if (DataContext is DaxEditorViewModel vm && _editor is not null)
                vm.Text = _editor.Text;
        };
        Sync();
    }

    private void Sync()
    {
        if (_editor is null) return;
        if (DataContext is DaxEditorViewModel vm && _editor.Text != vm.Text)
            _editor.Text = vm.Text;
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

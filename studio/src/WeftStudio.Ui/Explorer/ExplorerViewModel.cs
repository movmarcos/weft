// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using ReactiveUI;
using WeftStudio.App;

namespace WeftStudio.Ui.Explorer;

public sealed class ExplorerViewModel : ReactiveObject
{
    public ExplorerViewModel(ModelSession session)
    {
        Session = session;
        Roots = BuildRoots(session);
    }

    public ModelSession Session { get; }
    public ObservableCollection<TreeNode> Roots { get; }

    private static ObservableCollection<TreeNode> BuildRoots(ModelSession s)
    {
        var tables = new TreeNode("Tables");
        foreach (var t in s.Database.Model.Tables)
        {
            var node = new TreeNode(t.Name, t);
            foreach (var c in t.Columns)  node.Children.Add(new TreeNode(c.Name, c));
            foreach (var m in t.Measures) node.Children.Add(new TreeNode(m.Name, m));
            tables.Children.Add(node);
        }

        var measures = new TreeNode("Measures");
        foreach (var t in s.Database.Model.Tables)
            foreach (var m in t.Measures)
                measures.Children.Add(new TreeNode($"{t.Name}[{m.Name}]", m));

        var rels = new TreeNode("Relationships");
        foreach (var r in s.Database.Model.Relationships)
            rels.Children.Add(new TreeNode(r.Name, r));

        var roles = new TreeNode("Roles");
        foreach (var role in s.Database.Model.Roles)
            roles.Children.Add(new TreeNode(role.Name, role));

        return new ObservableCollection<TreeNode> { tables, measures, rels, roles };
    }
}

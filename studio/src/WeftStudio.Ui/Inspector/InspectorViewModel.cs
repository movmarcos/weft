// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using Microsoft.AnalysisServices.Tabular;
using ReactiveUI;

namespace WeftStudio.Ui.Inspector;

/// <summary>
/// Generic read-only inspector for any TOM object selected in the Explorer tree.
/// Reflects public scalar properties (primitives, strings, enums, DateTime, Guid,
/// nullable wrappers) into a flat name/value grid. Skips collections and complex
/// objects — those would clutter the panel for v0.1.x viewing.
/// </summary>
public sealed class InspectorViewModel : ReactiveObject
{
    public string ObjectType { get; }
    public string ObjectName { get; }
    public ObservableCollection<PropertyRow> Properties { get; } = new();

    public InspectorViewModel(object tomObject)
    {
        ObjectType = FriendlyTypeName(tomObject);
        ObjectName = ReadName(tomObject);
        PopulateProperties(tomObject);
    }

    private static string FriendlyTypeName(object o) => o switch
    {
        Table         => "TABLE",
        Measure       => "MEASURE",
        Column        => "COLUMN",
        Relationship  => "RELATIONSHIP",
        Partition     => "PARTITION",
        ModelRole     => "ROLE",
        Hierarchy     => "HIERARCHY",
        _             => o.GetType().Name.ToUpperInvariant(),
    };

    private static string ReadName(object o)
    {
        var prop = o.GetType().GetProperty("Name");
        var v = prop?.GetValue(o)?.ToString();
        return string.IsNullOrEmpty(v) ? "(unnamed)" : v;
    }

    private void PopulateProperties(object o)
    {
        var props = o.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => IsDisplayable(p.PropertyType))
            .OrderBy(p => p.Name);

        foreach (var p in props)
        {
            string value;
            try
            {
                value = Format(p.GetValue(o));
            }
            catch
            {
                // Some TOM properties throw on detached or partially-loaded objects.
                continue;
            }
            Properties.Add(new PropertyRow(p.Name, value));
        }
    }

    private static bool IsDisplayable(System.Type t)
    {
        var u = Nullable.GetUnderlyingType(t) ?? t;
        return u.IsPrimitive
            || u == typeof(string)
            || u == typeof(DateTime)
            || u == typeof(Guid)
            || u == typeof(decimal)
            || u.IsEnum;
    }

    private static string Format(object? value) => value switch
    {
        null            => "",
        string s        => s,
        DateTime dt     => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        bool b          => b ? "true" : "false",
        _               => value.ToString() ?? "",
    };
}

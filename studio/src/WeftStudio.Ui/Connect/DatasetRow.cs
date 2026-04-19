// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System;
using ReactiveUI;
using WeftStudio.App.Connections;

namespace WeftStudio.Ui.Connect;

public sealed class DatasetRow : ReactiveObject
{
    public DatasetRow(DatasetInfo info)
    {
        Info = info;
        Name = info.Name;
        SizeDisplay = info.SizeBytes is null ? "-" : FormatSize(info.SizeBytes.Value);
        UpdatedDisplay = info.LastUpdatedUtc is null ? "-" : RelativeAge(info.LastUpdatedUtc.Value);
        RefreshPolicy = info.RefreshPolicy ?? "-";
        Owner = info.Owner ?? "-";
    }

    public DatasetInfo Info { get; }
    public string Name { get; }
    public string SizeDisplay { get; }
    public string UpdatedDisplay { get; }
    public string RefreshPolicy { get; }
    public string Owner { get; }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1_048_576 => $"{bytes / 1024} KB",
        < 1_073_741_824 => $"{bytes / 1_048_576} MB",
        _ => $"{bytes / 1_073_741_824} GB",
    };

    private static string RelativeAge(DateTime utc)
    {
        var delta = DateTime.UtcNow - utc;
        if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes}m ago";
        if (delta.TotalHours   < 24) return $"{(int)delta.TotalHours}h ago";
        if (delta.TotalDays     < 7) return $"{(int)delta.TotalDays}d ago";
        return utc.ToString("yyyy-MM-dd");
    }
}

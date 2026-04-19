// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using Avalonia.Data.Converters;

namespace WeftStudio.Ui.Connect;

/// <summary>Returns true when the bound value is not null.</summary>
public sealed class NotNullConverter : IValueConverter
{
    public static readonly NotNullConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Returns true when the dialog state is <see cref="ConnectDialogState.Picker"/>.</summary>
public sealed class StateIsPickerConverter : IValueConverter
{
    public static readonly StateIsPickerConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ConnectDialogState s && s == ConnectDialogState.Picker;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Returns true when the dialog state is NOT <see cref="ConnectDialogState.Picker"/>.</summary>
public sealed class StateNotPickerConverter : IValueConverter
{
    public static readonly StateNotPickerConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not ConnectDialogState s || s != ConnectDialogState.Picker;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

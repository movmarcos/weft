// Copyright (c) Marcos Magri / Weft contributors. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using Avalonia.Data;
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

/// <summary>Two-way converter that maps an enum value to/from a bool for RadioButton binding.</summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public static readonly EnumEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // RadioButton sends true when selected; map back to the parameter's enum value.
        if (value is true && parameter is not null && targetType.IsEnum)
            return Enum.Parse(targetType, parameter.ToString()!, ignoreCase: false);
        return BindingOperations.DoNothing;
    }
}

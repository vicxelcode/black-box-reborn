using System;
using System.Globalization;
using UnityEngine;

namespace BlackBoxReborn.UI;

internal static class Format
{
    /// <summary>Cantidad con separador de miles según el idioma del juego, sin el símbolo.</summary>
    public static string Money(float value)
    {
        var rounded = Mathf.RoundToInt(value);
        try
        {
            return SemiFunc.DollarGetString(rounded);
        }
        catch (Exception)
        {
            return rounded.ToString("#,0", CultureInfo.InvariantCulture);
        }
    }

    public static string Dollars(float value) => "$" + Money(value);

    public static string Loss(float value) => value >= 0.5f ? "-$" + Money(value) : "$0";

    public static string Duration(float seconds)
    {
        if (seconds < 0f) seconds = 0f;
        var total = Mathf.FloorToInt(seconds);
        var h = total / 3600;
        var m = total / 60 % 60;
        var s = total % 60;
        return h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m:00}:{s:00}";
    }

    public static string Ago(float seconds)
    {
        var s = Mathf.Max(0, Mathf.FloorToInt(seconds));
        if (Loc.Spanish) return s < 60 ? $"hace {s} s" : $"hace {s / 60} min";
        return s < 60 ? $"{s}s ago" : $"{s / 60}m ago";
    }

    public static string Percent(float value) => Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture);
}

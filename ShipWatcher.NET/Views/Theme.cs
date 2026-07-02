using Terminal.Gui;

namespace ShipWatcher.NET.Views;

/// <summary>
/// Shared color schemes so the shell and views stay visually consistent.
/// Properties build fresh schemes on access because they need
/// Application.Driver, which only exists after Application.Init().
/// </summary>
public static class Theme
{
    /// <summary>White-on-black with a bright-cyan focus highlight.</summary>
    public static ColorScheme Dark => new()
    {
        Normal = Application.Driver.MakeAttribute(Color.White, Color.Black),
        Focus = Application.Driver.MakeAttribute(Color.Black, Color.BrightCyan),
        HotNormal = Application.Driver.MakeAttribute(Color.BrightGreen, Color.Black),
        HotFocus = Application.Driver.MakeAttribute(Color.Black, Color.BrightCyan),
    };

    /// <summary>Black-on-cyan header row.</summary>
    public static ColorScheme Header => new()
    {
        Normal = Application.Driver.MakeAttribute(Color.Black, Color.Cyan),
    };
}

using Terminal.Gui.Drawing;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace ShipWatcher.NET.Views;

/// <summary>
/// Shared color schemes so the shell and views stay visually consistent.
/// v2 Schemes are driver-independent, so these are plain object graphs.
/// </summary>
public static class Theme
{
    /// <summary>White-on-black with a bright-cyan focus highlight.</summary>
    public static Scheme Dark => new()
    {
        Normal = new Attribute(Color.White, Color.Black),
        Focus = new Attribute(Color.Black, Color.BrightCyan),
        HotNormal = new Attribute(Color.BrightGreen, Color.Black),
        HotFocus = new Attribute(Color.Black, Color.BrightCyan),
    };

    /// <summary>Black-on-cyan header row.</summary>
    public static Scheme Header => new()
    {
        Normal = new Attribute(Color.Black, Color.Cyan),
    };
}

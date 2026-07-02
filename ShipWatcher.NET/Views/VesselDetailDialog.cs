using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace ShipWatcher.NET.Views;

/// <summary>Modal dialog showing all known fields of a vessel. Shared by the table and map views.</summary>
internal static class VesselDetailDialog
{
    public static void Show(IApplication app, Vessel v)
    {
        using var dlg = new Dialog
        {
            Title = $"Vessel: {v.Name}",
            Width = 60,
            Height = 19,
        };

        var info = new Label
        {
            Text =
                $"MMSI:        {v.MMSI}\n" +
                $"Name:        {v.Name}\n" +
                $"Call Sign:   {v.CallSign}\n" +
                $"Ship Type:   {v.ShipTypeText}\n" +
                $"Position:    {v.CoordinateString}\n" +
                $"Speed:       {v.Speed:F1} knots\n" +
                $"Course:      {v.Course:F1}°\n" +
                $"Heading:     {(v.Heading == 511 ? "N/A" : $"{v.Heading}°")}\n" +
                $"Nav Status:  {v.NavStatusText}\n" +
                $"Destination: {v.Destination}\n" +
                $"Draught:     {(v.Draught > 0 ? $"{v.Draught:F1} m" : "N/A")}\n" +
                $"ETA:         {v.EtaText}\n" +
                $"Last Update: {v.LastUpdate:yyyy-MM-dd HH:mm:ss} UTC ({v.AgeText})",
            X = 1,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
        };

        var ok = new Button { Text = "Close", IsDefault = true };
        dlg.Add(info);
        dlg.AddButton(ok);
        app.Run(dlg);
    }
}

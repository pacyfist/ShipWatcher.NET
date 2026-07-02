using Terminal.Gui;

namespace ShipWatcher.NET.Views;

public class TableShipView : IShipWatcherView
{
    private readonly VesselStore _store;
    private readonly Label _headerLabel;
    private readonly ListView _vesselList;
    private List<Vessel> _sortedVessels = [];
    private List<string> _vesselRows = [];
    private long? _selectedMmsi;
    private int _scrollOffset;
    private bool _suppressSelectionEvents;

    public string ViewName => "TABLE";

    public event Action<Vessel?>? VesselSelected;

    public TableShipView(VesselStore store)
    {
        _store = store;

        _headerLabel = new Label(FormatRow("MMSI", "Name", "Position", "SOG", "COG", "HDG", "Status", "Destination"))
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            ColorScheme = Theme.Header,
            Visible = false,
        };

        _vesselList = new ListView
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 6,
            CanFocus = true,
            Visible = false,
            ColorScheme = Theme.Dark,
        };

        _vesselList.SelectedItemChanged += (args) =>
        {
            if (_suppressSelectionEvents)
                return;

            var idx = args.Item;
            if (idx >= 0 && idx < _sortedVessels.Count)
            {
                var vessel = _sortedVessels[idx];
                _selectedMmsi = vessel.MMSI;
                
                // Capture the relative screen offset (how many lines from the top of the list view)
                _scrollOffset = _vesselList.SelectedItem - _vesselList.TopItem;
                
                VesselSelected?.Invoke(vessel);
            }
        };

        _vesselList.OpenSelectedItem += (_) =>
        {
            var idx = _vesselList.SelectedItem;
            if (idx >= 0 && idx < _sortedVessels.Count)
                ShowVesselDetail(_sortedVessels[idx]);
        };
    }

    public IEnumerable<View> GetViews() => [_headerLabel, _vesselList];

    public void Activate()
    {
        _headerLabel.Visible = true;
        _vesselList.Visible = true;
    }

    public void Deactivate()
    {
        _headerLabel.Visible = false;
        _vesselList.Visible = false;
    }

    public void Refresh(string filter)
    {
        _sortedVessels = _store.Vessels.Values
            .Where(v => string.IsNullOrEmpty(filter) ||
                         v.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                         v.MMSI.ToString().Contains(filter))
            .OrderBy(v => v.MMSI)
            .ToList();

        _vesselRows = _sortedVessels
            .Select(v => FormatRow(
                v.MMSI.ToString(),
                Truncate(v.Name, 18),
                v.CoordinateString,
                $"{v.Speed:F1}",
                $"{v.Course:F1}",
                v.Heading == 511 ? "N/A" : $"{v.Heading}",
                Truncate(v.NavStatusText, 12),
                Truncate(v.Destination, 16)
            ))
            .ToList();

        // SetSource and the programmatic SelectedItem/TopItem assignments below can
        // fire SelectedItemChanged themselves; suppress the handler so the restore
        // doesn't clobber _selectedMmsi/_scrollOffset mid-flight.
        _suppressSelectionEvents = true;
        try
        {
            _vesselList.SetSource(_vesselRows);

            if (_selectedMmsi.HasValue)
            {
                var newIdx = _sortedVessels.FindIndex(v => v.MMSI == _selectedMmsi.Value);
                if (newIdx >= 0)
                {
                    // Update selection
                    _vesselList.SelectedItem = newIdx;

                    // Restore scroll position so the item stays at the same relative screen position
                    _vesselList.TopItem = Math.Max(0, newIdx - _scrollOffset);

                    // Ensure the detail panel in AppShell updates with fresh data
                    VesselSelected?.Invoke(_sortedVessels[newIdx]);
                }
            }
        }
        finally
        {
            _suppressSelectionEvents = false;
        }
    }

    public void SetViewFocus() => _vesselList.SetFocus();

    private static void ShowVesselDetail(Vessel v)
    {
        var dlg = new Dialog($"Vessel: {v.Name}", 60, 18);

        var info = new Label(
            $"MMSI:        {v.MMSI}\n" +
            $"Name:        {v.Name}\n" +
            $"Call Sign:   {v.CallSign}\n" +
            $"Ship Type:   {v.ShipTypeText}\n" +
            $"Position:    {v.CoordinateString}\n" +
            $"Speed:       {v.Speed:F1} knots\n" +
            $"Course:      {v.Course:F1}\u00b0\n" +
            $"Heading:     {(v.Heading == 511 ? "N/A" : $"{v.Heading}\u00b0")}\n" +
            $"Nav Status:  {v.NavStatusText}\n" +
            $"Destination: {v.Destination}\n" +
            $"Draught:     {(v.Draught > 0 ? $"{v.Draught:F1} m" : "N/A")}\n" +
            $"ETA:         {v.EtaText}\n" +
            $"Last Update: {v.LastUpdate:yyyy-MM-dd HH:mm:ss} UTC ({v.AgeText})"
        )
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 1,
        };

        var ok = new Button("Close", true);
        ok.Clicked += () => Application.RequestStop();
        dlg.Add(info);
        dlg.AddButton(ok);
        Application.Run(dlg);
    }

    private static string FormatRow(string mmsi, string name, string pos, string sog, string cog, string hdg, string status, string dest)
    {
        return $"{mmsi,-12}{name,-20}{pos,-26}{sog,6}{cog,7}{hdg,6}  {status,-14}{dest,-16}";
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "\u2026";
}

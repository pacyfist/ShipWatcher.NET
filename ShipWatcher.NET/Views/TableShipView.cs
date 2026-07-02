using System.Collections.ObjectModel;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace ShipWatcher.NET.Views;

public class TableShipView : IShipWatcherView
{
    private enum SortMode
    {
        Mmsi,
        Name,
        Speed,
        Updated,
    }

    /// <summary>
    /// ListView with an extra key binding: O cycles the sort mode. Declared the
    /// v2 way — a Command implementation plus a KeyBindings entry — so the key
    /// is discoverable/configurable like every built-in binding.
    /// </summary>
    private sealed class SortableListView : ListView
    {
        public Action? SortRequested { get; set; }

        public SortableListView()
        {
            AddCommand(Command.Toggle, () =>
            {
                SortRequested?.Invoke();
                return true;
            });
            KeyBindings.Add(Key.O, Command.Toggle);
        }
    }

    private readonly VesselStore _store;
    private readonly Label _headerLabel;
    private readonly SortableListView _vesselList;
    private List<Vessel> _sortedVessels = [];
    private long? _selectedMmsi;
    private int _scrollOffset;
    private bool _suppressSelectionEvents;
    private SortMode _sortMode = SortMode.Mmsi;
    private string _filter = "";

    public string ViewName => "TABLE";

    public event Action<Vessel?>? VesselSelected;

    public TableShipView(VesselStore store)
    {
        _store = store;

        _headerLabel = new Label
        {
            Text = HeaderText(),
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Visible = false,
        };
        _headerLabel.SetScheme(Theme.Header);

        _vesselList = new SortableListView
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(6),
            CanFocus = true,
            Visible = false,
            SortRequested = CycleSortMode,
        };
        _vesselList.SetScheme(Theme.Dark);

        _vesselList.ValueChanged += (_, _) =>
        {
            if (_suppressSelectionEvents)
                return;

            var idx = _vesselList.SelectedItem ?? -1;
            if (idx >= 0 && idx < _sortedVessels.Count)
            {
                var vessel = _sortedVessels[idx];
                _selectedMmsi = vessel.MMSI;

                // Capture the relative screen offset (lines from the top of the list view)
                _scrollOffset = idx - _vesselList.Viewport.Y;

                VesselSelected?.Invoke(vessel);
            }
        };

        _vesselList.Accepting += (_, e) =>
        {
            var idx = _vesselList.SelectedItem ?? -1;
            if (idx >= 0 && idx < _sortedVessels.Count)
            {
                ShowVesselDetail(_sortedVessels[idx]);
                e.Handled = true;
            }
        };
    }

    private void CycleSortMode()
    {
        var modes = Enum.GetValues<SortMode>();
        _sortMode = modes[((int)_sortMode + 1) % modes.Length];
        _headerLabel.Text = HeaderText();
        Refresh(_filter);
    }

    private string HeaderText() =>
        FormatRow("MMSI", "Name", "Position", "SOG", "COG", "HDG", "Status", "Destination") +
        $"  [O: sort by {_sortMode}]";

    private IOrderedEnumerable<Vessel> ApplySort(IEnumerable<Vessel> vessels) => _sortMode switch
    {
        SortMode.Name => vessels
            .OrderBy(v => string.IsNullOrEmpty(v.Name) ? 1 : 0) // named vessels first
            .ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(v => v.MMSI),
        SortMode.Speed => vessels.OrderByDescending(v => v.Speed).ThenBy(v => v.MMSI),
        SortMode.Updated => vessels.OrderByDescending(v => v.LastUpdate).ThenBy(v => v.MMSI),
        _ => vessels.OrderBy(v => v.MMSI),
    };

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
        _filter = filter;
        _sortedVessels = ApplySort(_store.Vessels.Values
            .Where(v => string.IsNullOrEmpty(filter) ||
                         v.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                         v.MMSI.ToString().Contains(filter)))
            .ToList();

        var rows = new ObservableCollection<string>(_sortedVessels
            .Select(v => FormatRow(
                v.MMSI.ToString(),
                Truncate(v.Name, 18),
                v.CoordinateString,
                $"{v.Speed:F1}",
                $"{v.Course:F1}",
                v.Heading == 511 ? "N/A" : $"{v.Heading}",
                Truncate(v.NavStatusText, 12),
                Truncate(v.Destination, 16)
            )));

        // SetSource and the programmatic SelectedItem/Viewport assignments below
        // fire selection events themselves; suppress the handler so the restore
        // doesn't clobber _selectedMmsi/_scrollOffset mid-flight.
        _suppressSelectionEvents = true;
        try
        {
            _vesselList.SetSource(rows);

            if (_selectedMmsi.HasValue)
            {
                var newIdx = _sortedVessels.FindIndex(v => v.MMSI == _selectedMmsi.Value);
                if (newIdx >= 0)
                {
                    // Update selection
                    _vesselList.SelectedItem = newIdx;

                    // Restore scroll position so the item stays at the same relative screen position
                    var top = Math.Max(0, newIdx - _scrollOffset);
                    _vesselList.Viewport = _vesselList.Viewport with { Y = top };

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

    private void ShowVesselDetail(Vessel v)
    {
        if (_vesselList.App is not { } app)
            return;

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

    private static string FormatRow(string mmsi, string name, string pos, string sog, string cog, string hdg, string status, string dest)
    {
        return $"{mmsi,-12}{name,-20}{pos,-26}{sog,6}{cog,7}{hdg,6}  {status,-14}{dest,-16}";
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";
}

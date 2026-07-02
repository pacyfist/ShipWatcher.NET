using Terminal.Gui.ViewBase;

namespace ShipWatcher.NET.Views;

public class MapShipView : IShipWatcherView
{
    private readonly MapView _mapView;

    public string ViewName => "MAP";

    public event Action<Vessel?>? VesselSelected;

    public MapShipView(VesselStore store)
    {
        _mapView = new MapView(store)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(6),
            Visible = true,
        };

        _mapView.VesselSelected += v => VesselSelected?.Invoke(v);
        _mapView.VesselOpened += v =>
        {
            if (_mapView.App is { } app)
                VesselDetailDialog.Show(app, v);
        };
    }

    public IEnumerable<View> GetViews() => [_mapView];

    public void Activate() => _mapView.Visible = true;

    public void Deactivate() => _mapView.Visible = false;

    public void Refresh(string filter)
    {
        _mapView.UpdateFilter(filter);

        // Keep the detail panel current with the selected vessel's latest data
        if (_mapView.SelectedVessel is { } selected)
            VesselSelected?.Invoke(selected);
    }

    public void SetViewFocus() => _mapView.SetFocus();

    public void SelectVessel(long? mmsi) => _mapView.SelectVessel(mmsi);
}

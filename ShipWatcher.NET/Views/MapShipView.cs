using Terminal.Gui;

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
            Height = Dim.Fill() - 5,
            Visible = true,
            CanFocus = true,
        };
    }

    public IEnumerable<View> GetViews() => [_mapView];

    public void Activate() => _mapView.Visible = true;

    public void Deactivate() => _mapView.Visible = false;

    public void Refresh(string filter)
    {
        _mapView.UpdateFilter(filter);
    }

    public void SetViewFocus() => _mapView.SetFocus();
}

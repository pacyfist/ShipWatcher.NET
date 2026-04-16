using Terminal.Gui;

namespace ShipWatcher.NET.Views;

/// <summary>
/// A pluggable UI view that can display vessel data.
/// Implement this interface and register in Program.cs to add a new view.
/// </summary>
public interface IShipWatcherView
{
    /// <summary>Short name for the status bar, e.g. "MAP", "TABLE".</summary>
    string ViewName { get; }

    /// <summary>Terminal.Gui views to add to the main window. Called once at startup.</summary>
    IEnumerable<View> GetViews();

    /// <summary>Show this view's elements.</summary>
    void Activate();

    /// <summary>Hide this view's elements.</summary>
    void Deactivate();

    /// <summary>Called on the refresh timer with the current filtered vessel list.</summary>
    void RefreshData(IReadOnlyList<Vessel> vessels);

    /// <summary>Set focus to this view's primary interactive element.</summary>
    void SetViewFocus();

    /// <summary>Raised when the user selects/highlights a vessel in this view.</summary>
    event Action<Vessel?>? VesselSelected;
}

using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using ShipWatcher.NET.Sources;
using ShipWatcher.NET.Views;

namespace ShipWatcher.NET;

public class AppShell(VesselStore vesselStore, IServiceProvider serviceProvider, int defaultSourceIndex = 0)
{
    /// <summary>Vessels without an update for this long are removed from the store.</summary>
    private static readonly TimeSpan VesselMaxAge = TimeSpan.FromMinutes(15);

    private List<IShipWatcherView> _views = [];
    private List<IAisDataSource> _sources = [];
    private readonly CancellationTokenSource _cts = new();

    private IApplication? _app;
    private IAisDataSource? _activeSource;
    private int _currentSourceIndex = defaultSourceIndex;
    private int _activeViewIndex;
    private string _nameFilter = "";

    public void Run()
    {
        _sources = serviceProvider.GetServices<IAisDataSource>().ToList();
        _activeSource = _sources[_currentSourceIndex];

        using IApplication app = Application.Create();
        _app = app;
        app.Init();

        // Create views after Init so driver-dependent state is available
        _views = serviceProvider.GetServices<IShipWatcherView>().ToList();

        using var win = new Window
        {
            Title = "ShipWatcher - Live AIS Vessel Tracker",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

        // Detail panel (shared across views), sits above the status bar
        var detailFrame = new FrameView
        {
            Title = "Vessel Detail",
            X = 0,
            Y = Pos.AnchorEnd(6),
            Width = Dim.Fill(),
            Height = 5,
        };
        detailFrame.SetScheme(Theme.Dark);

        var detailLabel = new Label
        {
            Text = "Select a vessel to see details (F2 to switch view)",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        detailFrame.Add(detailLabel);

        // Register all views
        foreach (var view in _views)
        {
            foreach (var guiView in view.GetViews())
                win.Add(guiView);

            view.VesselSelected += v =>
            {
                if (v is not null)
                {
                    detailLabel.Text =
                        $"MMSI: {v.MMSI}  Name: {v.Name}  Call: {v.CallSign}  Type: {v.ShipTypeText}\n" +
                        $"Pos: {v.CoordinateString}  SOG: {v.Speed:F1}kn  COG: {v.Course:F1}°  HDG: {v.Heading}°  Status: {v.NavStatusText}\n" +
                        $"Dest: {v.Destination}  Draught: {v.Draught:F1}m  ETA: {v.EtaText}  Updated: {v.AgeText}";
                }
            };
        }

        win.Add(detailFrame);

        // Activate the first view, deactivate the rest
        for (int i = 0; i < _views.Count; i++)
        {
            if (i == _activeViewIndex)
                _views[i].Activate();
            else
                _views[i].Deactivate();
        }

        // Status bar: app-wide actions as Shortcuts (the v2 idiom).
        // F2 cycles views because Tab is reserved for focus navigation in v2.
        var statusBar = new StatusBar(
        [
            new Shortcut(Key.Q.WithCtrl, "Quit", () => app.RequestStop()),
            new Shortcut(Key.F2, "Cycle View", CycleView),
            new Shortcut(Key.C, "Clear", () => { vesselStore.Clear(); RefreshActiveView(); }),
            new Shortcut(Key.R.WithCtrl, "Reconnect", Reconnect),
            new Shortcut(Key.F, "Filter", ShowFilterDialog),
            new Shortcut(Key.S, "Source", ShowSourceDialog),
        ]);
        win.Add(statusBar);

        // Refresh timers
        app.AddTimeout(TimeSpan.FromSeconds(2), () =>
        {
            RefreshActiveView();
            return true;
        });

        // Prune vessels that have gone quiet so the store doesn't grow forever
        app.AddTimeout(TimeSpan.FromSeconds(30), () =>
        {
            var removed = vesselStore.Prune(VesselMaxAge);
            if (removed > 0)
                RefreshActiveView();
            return true;
        });

        app.AddTimeout(TimeSpan.FromSeconds(1), () =>
        {
            var src = _activeSource;
            if (src is null) return true;

            var connected = src.IsConnected ? "CONNECTED" : "DISCONNECTED";
            var err = src.LastError != null ? $" | Err: {src.LastError}" : "";
            var viewName = _views[_activeViewIndex].ViewName;
            var sourceName = src.SourceName;
            win.Title = $"ShipWatcher - {sourceName} - {connected} | {viewName} | Total Vessels: {vesselStore.Count} | Msgs: {src.MessageCount}{err}";
            return true;
        });

        // Connect and run (ConnectAsync returns immediately; the connection is supervised)
        _ = _activeSource.ConnectAsync(_cts.Token);

        app.Run(win);

        // Stop all source activity; the sources themselves are disposed with
        // the DI container in Program.cs.
        _cts.Cancel();
        _cts.Dispose();
        _app = null;
    }

    private void CycleView()
    {
        _views[_activeViewIndex].Deactivate();
        _activeViewIndex = (_activeViewIndex + 1) % _views.Count;
        _views[_activeViewIndex].Activate();
        _views[_activeViewIndex].SetViewFocus();
        RefreshActiveView();
    }

    private void RefreshActiveView()
    {
        _views[_activeViewIndex].Refresh(_nameFilter);
    }

    private void ShowSourceDialog()
    {
        if (_app is not { } app)
            return;

        // Use the already resolved sources
        var descriptors = _sources.OfType<ISourceDescriptor>().ToList();
        var labels = descriptors.Select(d => d.DisplayLabel).ToArray();

        // Calculate dialog height based on max config fields
        var maxFields = descriptors.Max(d => d.ConfigFields.Count);
        var dlgHeight = 8 + labels.Length + maxFields;

        using var dlg = new Dialog
        {
            Title = "Select Data Source",
            Width = 60,
            Height = dlgHeight,
        };

        var selector = new OptionSelector
        {
            X = 1,
            Y = 0,
            Labels = labels,
            Value = _currentSourceIndex,
        };
        dlg.Add(selector);

        // Build config field UI for each source
        var fieldsBySource = new List<List<(SourceConfigField field, Label label, TextField textField)>>();
        int configY = labels.Length + 1;

        for (int i = 0; i < descriptors.Count; i++)
        {
            var fields = new List<(SourceConfigField, Label, TextField)>();
            int y = configY;

            foreach (var cf in descriptors[i].ConfigFields)
            {
                var lbl = new Label { Text = $"{cf.Label}:", X = 1, Y = y };
                var tf = new TextField { Text = cf.CurrentValue, X = 16, Y = y, Width = 38, Secret = cf.IsSensitive };
                lbl.Visible = i == _currentSourceIndex;
                tf.Visible = i == _currentSourceIndex;
                dlg.Add(lbl, tf);
                fields.Add((cf, lbl, tf));
                y++;
            }

            fieldsBySource.Add(fields);
        }

        selector.ValueChanged += (_, _) =>
        {
            var selected = selector.Value ?? _currentSourceIndex;
            for (int i = 0; i < fieldsBySource.Count; i++)
            {
                var visible = i == selected;
                foreach (var (_, lbl, tf) in fieldsBySource[i])
                {
                    lbl.Visible = visible;
                    tf.Visible = visible;
                }
            }
        };

        var connect = new Button { Text = "Connect", IsDefault = true };
        connect.Accepting += (_, e) =>
        {
            var selected = selector.Value ?? _currentSourceIndex;
            var descriptor = descriptors[selected];

            // Collect config values from text fields
            var configValues = new Dictionary<string, string>();
            foreach (var (field, _, tf) in fieldsBySource[selected])
            {
                configValues[field.Key] = tf.Text?.Trim() ?? "";
            }

            descriptor.ApplyConfig(configValues);
            var error = descriptor.ValidateConfig();
            if (error != null)
            {
                MessageBox.ErrorQuery(app, "Validation Error", error, "OK");
                e.Handled = true; // keep the dialog open
                return;
            }

            if (selected != _currentSourceIndex)
            {
                _activeSource?.Disconnect();
                _currentSourceIndex = selected;
                _activeSource = _sources[_currentSourceIndex];
                _ = _activeSource?.ConnectAsync(_cts.Token);
            }
            else if (configValues.Count > 0)
            {
                // Same source but config changed — reconnect
                _ = _activeSource?.ConnectAsync(_cts.Token);
            }

            // Not handled: the dialog closes itself on button accept
        };

        var cancel = new Button { Text = "Cancel" };

        dlg.AddButton(connect);
        dlg.AddButton(cancel);
        app.Run(dlg);
    }

    private void ShowFilterDialog()
    {
        if (_app is not { } app)
            return;

        using var dlg = new Dialog
        {
            Title = "Filter Vessels",
            Width = 50,
            Height = 8,
        };

        var lbl = new Label { Text = "Name/MMSI:", X = 1, Y = 0 };
        var tf = new TextField { Text = _nameFilter, X = 14, Y = 0, Width = 30 };

        var apply = new Button { Text = "Apply", IsDefault = true };
        apply.Accepting += (_, _) =>
        {
            _nameFilter = tf.Text ?? "";
            RefreshActiveView();
        };

        var clear = new Button { Text = "Clear" };
        clear.Accepting += (_, _) =>
        {
            _nameFilter = "";
            RefreshActiveView();
        };

        dlg.Add(lbl, tf);
        dlg.AddButton(apply);
        dlg.AddButton(clear);
        app.Run(dlg);
    }

    private void Reconnect()
    {
        // ConnectAsync tears down any existing connection itself
        _ = _activeSource?.ConnectAsync(_cts.Token);
    }
}

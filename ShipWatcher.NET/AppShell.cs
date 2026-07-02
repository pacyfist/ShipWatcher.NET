using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui;
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

    private IAisDataSource? _activeSource;
    private int _currentSourceIndex = defaultSourceIndex;
    private int _activeViewIndex;
    private string _nameFilter = "";

    public void Run()
    {
        _sources = serviceProvider.GetServices<IAisDataSource>().ToList();
        _activeSource = _sources[_currentSourceIndex];

        Application.Init();

        // Create views after Application.Init() so Application.Driver is available
        _views = serviceProvider.GetServices<IShipWatcherView>().ToList();

        var top = Application.Top;

        var win = new Window("ShipWatcher - Live AIS Vessel Tracker")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 1,
        };

        // Detail panel (shared across views)
        var darkScheme = new ColorScheme
        {
            Normal = Application.Driver.MakeAttribute(Color.White, Color.Black),
            Focus = Application.Driver.MakeAttribute(Color.Black, Color.BrightCyan),
            HotNormal = Application.Driver.MakeAttribute(Color.BrightGreen, Color.Black),
            HotFocus = Application.Driver.MakeAttribute(Color.Black, Color.BrightCyan),
        };

        var detailFrame = new FrameView("Vessel Detail")
        {
            X = 0,
            Y = Pos.AnchorEnd(5),
            Width = Dim.Fill(),
            Height = 5,
            ColorScheme = darkScheme,
        };

        var detailLabel = new Label("Select a vessel to see details (Tab to switch view)")
        {
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
                        $"Pos: {v.CoordinateString}  SOG: {v.Speed:F1}kn  COG: {v.Course:F1}\u00b0  HDG: {v.Heading}\u00b0  Status: {v.NavStatusText}\n" +
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

        // Status bar
        var statusBar = new StatusBar(new StatusItem[]
        {
            new(Key.Q | Key.CtrlMask, "~Ctrl+Q~ Quit", () => Application.RequestStop()),
            new(Key.Tab, "~Tab~ Cycle View", CycleView),
            new(Key.C, "~C~ Clear", () => { vesselStore.Clear(); RefreshActiveView(); }),
            new(Key.R | Key.CtrlMask, "~Ctrl+R~ Reconnect", async () => await Reconnect()),
            new(Key.F, "~F~ Filter", ShowFilterDialog),
            new(Key.S, "~S~ Source", ShowSourceDialog),
        });

        top.Add(win, statusBar);

        // Refresh timers
        Application.MainLoop.AddTimeout(TimeSpan.FromSeconds(2), (_) =>
        {
            RefreshActiveView();
            return true;
        });

        // Prune vessels that have gone quiet so the store doesn't grow forever
        Application.MainLoop.AddTimeout(TimeSpan.FromSeconds(30), (_) =>
        {
            var removed = vesselStore.Prune(VesselMaxAge);
            if (removed > 0)
                RefreshActiveView();
            return true;
        });

        Application.MainLoop.AddTimeout(TimeSpan.FromSeconds(1), (_) =>
        {
            var src = _activeSource;
            if (src is null) return true;

            var connected = src.IsConnected ? "CONNECTED" : "DISCONNECTED";
            var err = src.LastError != null ? $" | Err: {src.LastError}" : "";
            var viewName = _views[_activeViewIndex].ViewName;
            var sourceName = src.SourceName;
            win.Title = $"ShipWatcher - {sourceName} - {connected} | {viewName} | Total Vessels: {vesselStore.Vessels.Count} | Msgs: {src.MessageCount}{err}";
            return true;
        });

        // Connect and run
        _ = Task.Run(async () => await _activeSource.ConnectAsync(_cts.Token));

        Application.Run();
        Application.Shutdown();

        _activeSource?.Dispose();
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
        // Use the already resolved sources
        var descriptors = _sources.OfType<ISourceDescriptor>().ToList();
        
        var labels = descriptors.Select(d => (NStack.ustring)d.DisplayLabel).ToArray();

        // Calculate dialog height based on max config fields
        var maxFields = descriptors.Max(d => d.ConfigFields.Count);
        var dlgHeight = 8 + maxFields * 2;

        var dlg = new Dialog("Select Data Source", 60, dlgHeight);

        var radioGroup = new RadioGroup(labels)
        {
            X = 1,
            Y = 0,
            SelectedItem = _currentSourceIndex,
        };

        dlg.Add(radioGroup);

        // Build config field UI for each source
        var fieldsBySource = new List<List<(SourceConfigField field, Label label, TextField textField)>>();
        int configY = labels.Length + 1;

        for (int i = 0; i < descriptors.Count; i++)
        {
            var fields = new List<(SourceConfigField, Label, TextField)>();
            int y = configY;

            foreach (var cf in descriptors[i].ConfigFields)
            {
                var lbl = new Label($"{cf.Label}:") { X = 1, Y = y };
                var tf = new TextField(cf.CurrentValue) { X = 14, Y = y, Width = 42, Secret = cf.IsSensitive };
                lbl.Visible = i == _currentSourceIndex;
                tf.Visible = i == _currentSourceIndex;
                dlg.Add(lbl, tf);
                fields.Add((cf, lbl, tf));
                y++;
            }

            fieldsBySource.Add(fields);
        }

        radioGroup.SelectedItemChanged += (args) =>
        {
            for (int i = 0; i < fieldsBySource.Count; i++)
            {
                var visible = i == args.SelectedItem;
                foreach (var (_, lbl, tf) in fieldsBySource[i])
                {
                    lbl.Visible = visible;
                    tf.Visible = visible;
                }
            }
        };

        var connect = new Button("Connect", true);
        connect.Clicked += () =>
        {
            var selected = radioGroup.SelectedItem;
            var descriptor = descriptors[selected];

            // Collect config values from text fields
            var configValues = new Dictionary<string, string>();
            foreach (var (field, _, tf) in fieldsBySource[selected])
            {
                configValues[field.Key] = tf.Text?.ToString()?.Trim() ?? "";
            }

            descriptor.ApplyConfig(configValues);
            var error = descriptor.ValidateConfig();
            if (error != null)
            {
                MessageBox.ErrorQuery("Validation Error", error, "OK");
                return;
            }

            if (selected != _currentSourceIndex)
            {
                _activeSource?.Disconnect();
                _currentSourceIndex = selected;
                _activeSource = _sources[_currentSourceIndex];
                if (_activeSource != null)
                {
                    _ = Task.Run(async () => await _activeSource.ConnectAsync(_cts.Token));
                }
            }
            else if (configValues.Count > 0)
            {
                // Same source but config changed — reconnect
                _activeSource?.Disconnect();
                if (_activeSource != null)
                {
                    _ = Task.Run(async () => await _activeSource.ConnectAsync(_cts.Token));
                }
            }

            Application.RequestStop();
        };

        var cancel = new Button("Cancel");
        cancel.Clicked += () => Application.RequestStop();

        dlg.AddButton(connect);
        dlg.AddButton(cancel);
        Application.Run(dlg);
    }

    private void ShowFilterDialog()
    {
        var dlg = new Dialog("Filter Vessels", 50, 8);
        var lbl = new Label("Name/MMSI:") { X = 1, Y = 0 };
        var tf = new TextField(_nameFilter) { X = 14, Y = 0, Width = 30 };

        var apply = new Button("Apply", true);
        apply.Clicked += () =>
        {
            _nameFilter = tf.Text?.ToString() ?? "";
            RefreshActiveView();
            Application.RequestStop();
        };

        var clear = new Button("Clear");
        clear.Clicked += () =>
        {
            _nameFilter = "";
            RefreshActiveView();
            Application.RequestStop();
        };

        dlg.Add(lbl, tf);
        dlg.AddButton(apply);
        dlg.AddButton(clear);
        Application.Run(dlg);
    }

    private async Task Reconnect()
    {
        var src = _activeSource;
        if (src is null) return;

        src.Disconnect();
        await Task.Run(async () => await src.ConnectAsync(_cts.Token));
    }
}

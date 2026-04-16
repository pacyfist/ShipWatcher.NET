using Terminal.Gui;
using ShipWatcher.NET;

// --- Configuration ---
var apiKey = Environment.GetEnvironmentVariable("AISSTREAM_API_KEY") ?? "";

// Default: worldwide bounding box. Override with env vars if desired.
var latMin = double.TryParse(Environment.GetEnvironmentVariable("SHIPWATCHER_LAT_MIN"), out var la1) ? la1 : -90.0;
var lonMin = double.TryParse(Environment.GetEnvironmentVariable("SHIPWATCHER_LON_MIN"), out var lo1) ? lo1 : -180.0;
var latMax = double.TryParse(Environment.GetEnvironmentVariable("SHIPWATCHER_LAT_MAX"), out var la2) ? la2 : 90.0;
var lonMax = double.TryParse(Environment.GetEnvironmentVariable("SHIPWATCHER_LON_MAX"), out var lo2) ? lo2 : 180.0;

double[][][] bbox = [[[latMin, lonMin], [latMax, lonMax]]];

using var cts = new CancellationTokenSource();

// --- Data source management ---
// Source 0 = aisstream.io (requires API key), Source 1 = Kystverket (open, Norway only)
int currentSourceIndex = 0;
IAisDataSource? activeSource = null;

IAisDataSource CreateSource(int index)
{
    return index switch
    {
        0 => new AisClient(apiKey, bbox),
        1 => new KystverketAisClient(),
        _ => throw new ArgumentOutOfRangeException()
    };
}

string[] sourceNames = ["aisstream.io", "Kystverket (Norway)"];

// Start with aisstream.io if API key is available, otherwise Kystverket
if (string.IsNullOrWhiteSpace(apiKey))
{
    currentSourceIndex = 1;
}

activeSource = CreateSource(currentSourceIndex);

Application.Init();

var top = Application.Top;

// --- Main window ---
var win = new Window("ShipWatcher - Live AIS Vessel Tracker")
{
    X = 0,
    Y = 0,
    Width = Dim.Fill(),
    Height = Dim.Fill() - 1
};

// --- Map view ---
var mapView = new MapView()
{
    X = 0,
    Y = 0,
    Width = Dim.Fill(),
    Height = Dim.Fill() - 5,
    Visible = true,
};

// --- Vessel table ---
var headerLabel = new Label(FormatRow("MMSI", "Name", "Position", "SOG", "COG", "HDG", "Status", "Destination"))
{
    X = 0,
    Y = 0,
    Width = Dim.Fill(),
    ColorScheme = new ColorScheme
    {
        Normal = Application.Driver.MakeAttribute(Color.Black, Color.Cyan),
    },
    Visible = false,
};

var darkScheme = new ColorScheme
{
    Normal = Application.Driver.MakeAttribute(Color.White, Color.Black),
    Focus = Application.Driver.MakeAttribute(Color.Black, Color.BrightCyan),
    HotNormal = Application.Driver.MakeAttribute(Color.BrightGreen, Color.Black),
    HotFocus = Application.Driver.MakeAttribute(Color.Black, Color.BrightCyan),
};

var vesselList = new ListView()
{
    X = 0,
    Y = 1,
    Width = Dim.Fill(),
    Height = Dim.Fill() - 6,
    CanFocus = true,
    Visible = false,
    ColorScheme = darkScheme,
};

// --- Detail panel ---
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

win.Add(mapView, headerLabel, vesselList, detailFrame);

// --- Data state ---
var vesselRows = new List<string>();
var sortedVessels = new List<Vessel>();
string nameFilter = "";
bool showMap = true;

// --- Toggle between map and table ---
void ToggleView()
{
    showMap = !showMap;
    mapView.Visible = showMap;
    headerLabel.Visible = !showMap;
    vesselList.Visible = !showMap;

    if (showMap)
        mapView.SetFocus();
    else
        vesselList.SetFocus();
}

// --- Switch data source via dialog ---
void ShowSourceDialog()
{
    var dlg = new Dialog("Select Data Source", 60, 14);

    var aisStreamLabel = "aisstream.io (global, requires API key)";
    var kystverketLabel = "Kystverket (Norway, open data)";

    var radioGroup = new RadioGroup([aisStreamLabel, kystverketLabel])
    {
        X = 1,
        Y = 0,
        SelectedItem = currentSourceIndex,
    };

    var apiKeyLabel = new Label("API Key:") { X = 1, Y = 3 };
    var apiKeyField = new TextField(apiKey) { X = 11, Y = 3, Width = 44 };

    radioGroup.SelectedItemChanged += (args) =>
    {
        apiKeyLabel.Visible = args.SelectedItem == 0;
        apiKeyField.Visible = args.SelectedItem == 0;
    };

    apiKeyLabel.Visible = radioGroup.SelectedItem == 0;
    apiKeyField.Visible = radioGroup.SelectedItem == 0;

    var connect = new Button("Connect", true);
    connect.Clicked += () =>
    {
        var selected = radioGroup.SelectedItem;
        var enteredKey = apiKeyField.Text?.ToString()?.Trim() ?? "";

        if (selected == 0 && string.IsNullOrWhiteSpace(enteredKey))
        {
            MessageBox.ErrorQuery("API Key Required", "Please enter an aisstream.io API key.", "OK");
            return;
        }

        bool keyChanged = selected == 0 && enteredKey != apiKey;
        if (selected == 0)
            apiKey = enteredKey;

        if (selected != currentSourceIndex || keyChanged)
        {
            activeSource?.Disconnect();
            activeSource?.Dispose();
            currentSourceIndex = selected;
            activeSource = CreateSource(currentSourceIndex);
            _ = Task.Run(async () => await activeSource.ConnectAsync(cts.Token));
        }
        Application.RequestStop();
    };

    var cancel = new Button("Cancel");
    cancel.Clicked += () => Application.RequestStop();

    dlg.Add(radioGroup, apiKeyLabel, apiKeyField);
    dlg.AddButton(connect);
    dlg.AddButton(cancel);
    Application.Run(dlg);
}

// --- Status bar ---
var statusBar = new StatusBar(new StatusItem[]
{
    new (Key.Q | Key.CtrlMask, "~Ctrl+Q~ Quit", () => Application.RequestStop()),
    new (Key.Tab, "~Tab~ Map/Table", ToggleView),
    new (Key.R | Key.CtrlMask, "~Ctrl+R~ Reconnect", async () => await Reconnect()),
    new (Key.F, "~F~ Filter", ShowFilterDialog),
    new (Key.S, "~S~ Source", ShowSourceDialog),
});

top.Add(win, statusBar);

vesselList.OpenSelectedItem += (_) =>
{
    var idx = vesselList.SelectedItem;
    if (idx >= 0 && idx < sortedVessels.Count)
        ShowVesselDetail(sortedVessels[idx]);
};

vesselList.SelectedItemChanged += (args) =>
{
    var idx = args.Item;
    if (idx >= 0 && idx < sortedVessels.Count)
    {
        var v = sortedVessels[idx];
        detailLabel.Text =
            $"MMSI: {v.MMSI}  Name: {v.Name}  Call: {v.CallSign}  Dest: {v.Destination}\n" +
            $"Pos: {v.CoordinateString}  SOG: {v.Speed:F1}kn  COG: {v.Course:F1}\u00b0  HDG: {v.Heading}\u00b0  Status: {v.NavStatusText}\n" +
            $"Last Update: {v.LastUpdate}";
    }
};

// --- UI refresh timer ---
var refreshTimer = Application.MainLoop.AddTimeout(TimeSpan.FromSeconds(2), (_) =>
{
    RefreshTable();
    RefreshMap();
    return true;
});

// --- Status update timer ---
var statusTimer = Application.MainLoop.AddTimeout(TimeSpan.FromSeconds(1), (_) =>
{
    var src = activeSource;
    if (src is null) return true;

    var connected = src.IsConnected ? "CONNECTED" : "DISCONNECTED";
    var err = src.LastError != null ? $" | Err: {src.LastError}" : "";
    var view = showMap ? "MAP" : "TABLE";
    var sourceName = src.SourceName;
    win.Title = $"ShipWatcher - {sourceName} - {connected} | {view} | Vessels: {src.Vessels.Count} | Msgs: {src.MessageCount}{err}";
    return true;
});

// --- Connect and run ---
_ = Task.Run(async () =>
{
    await activeSource.ConnectAsync(cts.Token);
});

Application.Run();
Application.Shutdown();

activeSource?.Dispose();

return 0;

// --- Helper functions ---

void RefreshTable()
{
    var src = activeSource;
    if (src is null) return;

    var vessels = src.Vessels.Values
        .Where(v => string.IsNullOrEmpty(nameFilter) ||
                     v.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase) ||
                     v.MMSI.ToString().Contains(nameFilter))
        .OrderByDescending(v => v.LastUpdate)
        .Take(500)
        .ToList();

    sortedVessels = vessels;
    vesselRows = vessels
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

    var selected = vesselList.SelectedItem;
    vesselList.SetSource(vesselRows);
    if (selected < vesselRows.Count)
        vesselList.SelectedItem = selected;
}

void RefreshMap()
{
    var src = activeSource;
    if (src is null) return;

    var vessels = src.Vessels.Values
        .Where(v => string.IsNullOrEmpty(nameFilter) ||
                     v.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase) ||
                     v.MMSI.ToString().Contains(nameFilter))
        .OrderByDescending(v => v.LastUpdate)
        .Take(2000)
        .ToList();

    mapView.UpdateVessels(vessels);
}

static string FormatRow(string mmsi, string name, string pos, string sog, string cog, string hdg, string status, string dest)
{
    return $"{mmsi,-12}{name,-20}{pos,-26}{sog,6}{cog,7}{hdg,6}  {status,-14}{dest,-16}";
}

static string Truncate(string s, int max) =>
    s.Length <= max ? s : s[..(max - 1)] + "\u2026";

void ShowVesselDetail(Vessel v)
{
    var dlg = new Dialog($"Vessel: {v.Name}", 60, 14);

    var info = new Label(
        $"MMSI:        {v.MMSI}\n" +
        $"Name:        {v.Name}\n" +
        $"Call Sign:   {v.CallSign}\n" +
        $"Position:    {v.CoordinateString}\n" +
        $"Speed:       {v.Speed:F1} knots\n" +
        $"Course:      {v.Course:F1}\u00b0\n" +
        $"Heading:     {(v.Heading == 511 ? "N/A" : $"{v.Heading}\u00b0")}\n" +
        $"Nav Status:  {v.NavStatusText}\n" +
        $"Destination:  {v.Destination}\n" +
        $"Last Update: {v.LastUpdate}"
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

void ShowFilterDialog()
{
    var dlg = new Dialog("Filter Vessels", 50, 8);
    var lbl = new Label("Name/MMSI:") { X = 1, Y = 0 };
    var tf = new TextField(nameFilter) { X = 14, Y = 0, Width = 30 };
    var apply = new Button("Apply", true);
    apply.Clicked += () =>
    {
        nameFilter = tf.Text?.ToString() ?? "";
        RefreshTable();
        RefreshMap();
        Application.RequestStop();
    };
    var clear = new Button("Clear");
    clear.Clicked += () =>
    {
        nameFilter = "";
        RefreshTable();
        RefreshMap();
        Application.RequestStop();
    };
    dlg.Add(lbl, tf);
    dlg.AddButton(apply);
    dlg.AddButton(clear);
    Application.Run(dlg);
}

async Task Reconnect()
{
    var src = activeSource;
    if (src is null) return;

    src.Disconnect();
    await Task.Run(async () => await src.ConnectAsync(cts.Token));
}

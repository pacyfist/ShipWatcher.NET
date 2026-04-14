using Terminal.Gui;
using ShipWatcher.NET;

// --- Configuration ---
var apiKey = Environment.GetEnvironmentVariable("AISSTREAM_API_KEY") ?? "";
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("Set AISSTREAM_API_KEY environment variable to your aisstream.io API key.");
    Console.Error.WriteLine("  export AISSTREAM_API_KEY=your_key_here");
    Console.Error.WriteLine("Get a free key at https://aisstream.io");
    return 1;
}

// Default: worldwide bounding box. Override with env vars if desired.
var latMin = double.TryParse(Environment.GetEnvironmentVariable("SHIPWATCHER_LAT_MIN"), out var la1) ? la1 : -90.0;
var lonMin = double.TryParse(Environment.GetEnvironmentVariable("SHIPWATCHER_LON_MIN"), out var lo1) ? lo1 : -180.0;
var latMax = double.TryParse(Environment.GetEnvironmentVariable("SHIPWATCHER_LAT_MAX"), out var la2) ? la2 : 90.0;
var lonMax = double.TryParse(Environment.GetEnvironmentVariable("SHIPWATCHER_LON_MAX"), out var lo2) ? lo2 : 180.0;

double[][][] bbox = [[[latMin, lonMin], [latMax, lonMax]]];

using var cts = new CancellationTokenSource();
using var client = new AisClient(apiKey, bbox);

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

// --- Status bar ---
var statusBar = new StatusBar(new StatusItem[]
{
    new (Key.Q | Key.CtrlMask, "~Ctrl+Q~ Quit", () => Application.RequestStop()),
    new (Key.Tab, "~Tab~ Map/Table", ToggleView),
    new (Key.R | Key.CtrlMask, "~Ctrl+R~ Reconnect", async () => await Reconnect()),
    new (Key.F, "~F~ Filter", ShowFilterDialog),
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
    var connected = client.IsConnected ? "CONNECTED" : "DISCONNECTED";
    var err = client.LastError != null ? $" | Err: {client.LastError}" : "";
    var view = showMap ? "MAP" : "TABLE";
    win.Title = $"ShipWatcher - {connected} | {view} | Vessels: {client.Vessels.Count} | Msgs: {client.MessageCount}{err}";
    return true;
});

// --- Connect and run ---
_ = Task.Run(async () =>
{
    await client.ConnectAsync(cts.Token);
});

Application.Run();
Application.Shutdown();

return 0;

// --- Helper functions ---

void RefreshTable()
{
    var vessels = client.Vessels.Values
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
    var vessels = client.Vessels.Values
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
    client.Dispose();
    using var newClient = new AisClient(apiKey, bbox);
    await newClient.ConnectAsync(cts.Token);
}

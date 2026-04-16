using ShipWatcher.NET;
using ShipWatcher.NET.Sources;
using ShipWatcher.NET.Views;

// --- Configuration ---
var apiKey = Environment.GetEnvironmentVariable("AISSTREAM_API_KEY") ?? "";

var latMin = double.TryParse(Environment.GetEnvironmentVariable("SHIPWATCHER_LAT_MIN"), out var la1) ? la1 : -90.0;
var lonMin = double.TryParse(Environment.GetEnvironmentVariable("SHIPWATCHER_LON_MIN"), out var lo1) ? lo1 : -180.0;
var latMax = double.TryParse(Environment.GetEnvironmentVariable("SHIPWATCHER_LAT_MAX"), out var la2) ? la2 : 90.0;
var lonMax = double.TryParse(Environment.GetEnvironmentVariable("SHIPWATCHER_LON_MAX"), out var lo2) ? lo2 : 180.0;

double[][][] bbox = [[[latMin, lonMin], [latMax, lonMax]]];

// --- Register data sources ---
// To add a new source: implement IAisDataSource + ISourceDescriptor, add a factory here.
var sourceFactories = new List<Func<IAisDataSource>>
{
    () => new AisClient(apiKey, bbox),
    () => new KystverketAisClient(),
};

// --- Register views ---
// To add a new view: implement IShipWatcherView, add a factory here.
var viewFactories = new List<Func<IShipWatcherView>>
{
    () => new MapShipView(),
    () => new TableShipView(),
};

int defaultSource = string.IsNullOrWhiteSpace(apiKey) ? 1 : 0;

var app = new AppShell(sourceFactories, viewFactories, defaultSource);
app.Run();

return 0;

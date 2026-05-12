using Microsoft.Extensions.DependencyInjection;
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

var services = new ServiceCollection();

// --- Services ---
services.AddSingleton<VesselStore>();

// --- Data Sources ---
services.AddSingleton<IAisDataSource>(sp => new AisClient(sp.GetRequiredService<VesselStore>(), apiKey, bbox));
services.AddSingleton<IAisDataSource>(sp => new KystverketAisClient(sp.GetRequiredService<VesselStore>()));

// --- Views ---
services.AddTransient<IShipWatcherView, MapShipView>();
services.AddTransient<IShipWatcherView, TableShipView>();

// --- App Shell ---
int defaultSource = string.IsNullOrWhiteSpace(apiKey) ? 1 : 0;
services.AddSingleton(sp => new AppShell(sp.GetRequiredService<VesselStore>(), sp, defaultSource));

var serviceProvider = services.BuildServiceProvider();

var app = serviceProvider.GetRequiredService<AppShell>();
app.Run();

return 0;

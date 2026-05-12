using Microsoft.Extensions.DependencyInjection;
using ShipWatcher.NET;
using ShipWatcher.NET.Sources;
using ShipWatcher.NET.Views;

var apiKey = Environment.GetEnvironmentVariable("BMA_API_KEY") ?? "";

var latMin = double.TryParse(Environment.GetEnvironmentVariable("SHIPWATCHER_LAT_MIN"), out var val) ? val : 50.0;
var latMax = double.TryParse(Environment.GetEnvironmentVariable("SHIPWATCHER_LAT_MAX"), out var val2) ? val2 : 75.0;
var lonMin = double.TryParse(Environment.GetEnvironmentVariable("SHIPWATCHER_LON_MIN"), out var val3) ? val3 : -10.0;
var lonMax = double.TryParse(Environment.GetEnvironmentVariable("SHIPWATCHER_LON_MAX"), out var val4) ? val4 : 35.0;

double[][][] bbox = [[[latMin, lonMin], [latMax, lonMax]]];

var services = new ServiceCollection();

// --- Data Sources ---
services.AddSingleton<IAisDataSource>(sp => new AisClient(apiKey, bbox));
services.AddSingleton<IAisDataSource, KystverketAisClient>();

// --- Views ---
services.AddTransient<IShipWatcherView, MapShipView>();
services.AddTransient<IShipWatcherView, TableShipView>();

// --- App Shell ---
int defaultSource = string.IsNullOrWhiteSpace(apiKey) ? 1 : 0;
services.AddSingleton(sp => new AppShell(sp, defaultSource));

var serviceProvider = services.BuildServiceProvider();

var app = serviceProvider.GetRequiredService<AppShell>();
app.Run();

return 0;

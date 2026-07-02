using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using ShipWatcher.NET;
using ShipWatcher.NET.Sources;
using ShipWatcher.NET.Views;

// --- Logging ---
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(
        Path.Combine(AppContext.BaseDirectory, "shipwatcher.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    // --- Configuration ---
    var apiKey = Environment.GetEnvironmentVariable("AISSTREAM_API_KEY") ?? "";

    var latMin = ParseEnvDouble("SHIPWATCHER_LAT_MIN", -90.0);
    var lonMin = ParseEnvDouble("SHIPWATCHER_LON_MIN", -180.0);
    var latMax = ParseEnvDouble("SHIPWATCHER_LAT_MAX", 90.0);
    var lonMax = ParseEnvDouble("SHIPWATCHER_LON_MAX", 180.0);

    double[][][] bbox = [[[latMin, lonMin], [latMax, lonMax]]];

    var services = new ServiceCollection();

    // --- Services ---
    services.AddSingleton<VesselStore>();

    // --- Data Sources ---
    services.AddSingleton<IAisDataSource>(sp => new AisClient(sp.GetRequiredService<VesselStore>(), apiKey, bbox));
    services.AddSingleton<IAisDataSource>(sp => new KystverketAisClient(sp.GetRequiredService<VesselStore>()));
    services.AddSingleton<IAisDataSource>(sp => new DigitrafficAisClient(sp.GetRequiredService<VesselStore>()));

    // --- Views ---
    services.AddTransient<IShipWatcherView, MapShipView>();
    services.AddTransient<IShipWatcherView, TableShipView>();

    // --- App Shell ---
    // 0: AisStream (requires key), 1: Kystverket (free), 2: Digitraffic (free)
    int defaultSource = string.IsNullOrWhiteSpace(apiKey) ? 1 : 0;
    services.AddSingleton(sp => new AppShell(sp.GetRequiredService<VesselStore>(), sp, defaultSource));

    // Disposing the provider disposes every singleton source on the way out
    await using var serviceProvider = services.BuildServiceProvider();

    var app = serviceProvider.GetRequiredService<AppShell>();
    app.Run();

    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

static double ParseEnvDouble(string name, double fallback)
{
    var raw = Environment.GetEnvironmentVariable(name);
    if (string.IsNullOrWhiteSpace(raw))
        return fallback;

    if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        return value;

    Log.Warning("Ignoring {EnvVar}={Value}: not a valid invariant-culture number, using {Fallback}",
        name, raw, fallback);
    return fallback;
}

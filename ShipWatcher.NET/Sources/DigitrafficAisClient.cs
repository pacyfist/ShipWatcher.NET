using System.Net;
using System.Net.Http.Json;
using Serilog;

namespace ShipWatcher.NET.Sources;

/// <summary>
/// AIS data source for Finland (Digitraffic)
/// API: https://www.digitraffic.fi/en/marine-traffic/
/// No registration or API key required.
/// </summary>
public class DigitrafficAisClient(VesselStore store) : IAisDataSource, ISourceDescriptor
{
    private readonly HttpClient _http = new(new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    })
    {
        Timeout = TimeSpan.FromMinutes(2)
    };

    private CancellationTokenSource? _cts;
    private static readonly ILogger Log = Serilog.Log.ForContext<DigitrafficAisClient>();

    public int MessageCount { get; private set; }
    public bool IsConnected { get; private set; }
    public string? LastError { get; private set; }
    public string SourceName => "Digitraffic (Finland)";

    public event Action? OnDataUpdated;

    // ISourceDescriptor
    public string DisplayLabel => "Digitraffic (Finland, open data)";
    public IReadOnlyList<SourceConfigField> ConfigFields => [];
    public string? ValidateConfig() => null;
    public void ApplyConfig(IReadOnlyDictionary<string, string> values) { }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        IsConnected = true;

        // Required headers for Digitraffic API
        _http.DefaultRequestHeaders.Clear();
        _http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        _http.DefaultRequestHeaders.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));
        _http.DefaultRequestHeaders.Add("User-Agent", "ShipWatcher.NET/1.0 (https://github.com/eich/ShipWatcher.NET)");

        Log.Information("Starting Digitraffic polling (Finland)");
        _ = Task.Run(() => PollLoop(_cts.Token), _cts.Token);
        await Task.CompletedTask;
    }

    public void Disconnect()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        IsConnected = false;
    }

    private async Task PollLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                Log.Debug("Polling Digitraffic locations...");
                // 1. Fetch latest locations (GeoJSON)
                var locationCollection = await _http.GetFromJsonAsync<DigitrafficLocationCollection>(
                    "https://meri.digitraffic.fi/api/ais/v1/locations", ct);

                if (locationCollection?.Features != null)
                {
                    Log.Debug("Received {Count} vessel locations from Digitraffic", locationCollection.Features.Count);
                    foreach (var feature in locationCollection.Features)
                    {
                        UpdateVessel(feature);
                        MessageCount++;
                    }
                }

                Log.Debug("Polling Digitraffic vessel metadata...");
                // 2. Fetch vessel metadata (names, destinations)
                var vessels = await _http.GetFromJsonAsync<List<DigitrafficVessel>>(
                    "https://meri.digitraffic.fi/api/ais/v1/vessels", ct);

                if (vessels != null)
                {
                    Log.Debug("Received {Count} vessel metadata entries from Digitraffic", vessels.Count);
                    foreach (var v in vessels)
                    {
                        UpdateVesselMetadata(v);
                    }
                }

                OnDataUpdated?.Invoke();
                LastError = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LastError = ex.Message;
                Log.Error(ex, "Digitraffic poll failed");
            }

            // Poll every 60 seconds
            await Task.Delay(TimeSpan.FromSeconds(60), ct);
        }
    }

    private void UpdateVessel(DigitrafficFeature feature)
    {
        var props = feature.Properties;
        var coords = feature.Geometry.Coordinates;

        if (coords.Length < 2) return;

        store.Upsert(feature.Mmsi, vessel => vessel with
        {
            Longitude = coords[0],
            Latitude = coords[1],
            Speed = props.Sog ?? 0,
            Course = props.Cog ?? 0,
            Heading = props.Heading ?? 511,
            NavStatus = props.NavStat ?? 15,
            LastUpdate = DateTimeOffset.UtcNow,
            ProviderTimestamp = props.TimestampExternal > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(props.TimestampExternal).ToString("yyyy-MM-dd HH:mm:ss")
                : vessel.ProviderTimestamp,
        });
    }

    private void UpdateVesselMetadata(DigitrafficVessel v)
    {
        // The metadata endpoint lists the whole vessel registry; only enrich
        // vessels we actually have a position for, so pruning isn't fighting
        // thousands of position-less entries every poll.
        store.UpdateIfExists(v.Mmsi, vessel => vessel with
        {
            Name = string.IsNullOrWhiteSpace(v.Name) ? vessel.Name : v.Name.Trim(),
            CallSign = string.IsNullOrWhiteSpace(v.CallSign) ? vessel.CallSign : v.CallSign.Trim(),
            Destination = string.IsNullOrWhiteSpace(v.Destination) ? vessel.Destination : v.Destination.Trim(),
            ShipType = v.ShipType ?? vessel.ShipType,
        });
    }

    public void Dispose()
    {
        Disconnect();
        _http.Dispose();
    }
}

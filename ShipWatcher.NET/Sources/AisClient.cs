using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Serilog;

namespace ShipWatcher.NET.Sources;

public class AisClient(VesselStore store, string apiKey, double[][][] boundingBoxes) : AisSourceBase, ISourceDescriptor
{
    private string _apiKey = apiKey;
    private readonly double[][][] _boundingBoxes = boundingBoxes;
    private ClientWebSocket? _ws;

    protected override ILogger Log { get; } = Serilog.Log.ForContext<AisClient>();

    public override bool IsConnected => _ws?.State == WebSocketState.Open;
    public override string SourceName => "aisstream.io";

    // ISourceDescriptor
    public string DisplayLabel => "aisstream.io (global, requires API key)";

    public IReadOnlyList<SourceConfigField> ConfigFields =>
    [
        new("apiKey", "API Key", _apiKey, IsSensitive: true)
    ];

    public string? ValidateConfig() =>
        string.IsNullOrWhiteSpace(_apiKey) ? "API key is required" : null;

    public void ApplyConfig(IReadOnlyDictionary<string, string> values)
    {
        if (values.TryGetValue("apiKey", out var key))
            _apiKey = key;
    }

    protected override async Task ReceiveAsync(CancellationToken ct)
    {
        var ws = new ClientWebSocket();
        _ws = ws;

        Log.Information("Connecting to aisstream.io");
        await ws.ConnectAsync(new Uri("wss://stream.aisstream.io/v0/stream"), ct);

        var subscription = new AisSubscription(
            _apiKey,
            _boundingBoxes,
            ["PositionReport", "ShipStaticData", "StandardClassBPositionReport"]
        );

        var json = JsonSerializer.Serialize(subscription);
        await ws.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, ct);
        Log.Information("Subscription sent. Starting receive loop");

        var buffer = new byte[8192];
        var healthy = false;

        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;

            do
            {
                result = await ws.ReceiveAsync(buffer, ct);
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close)
                return;

            ProcessMessage(Encoding.UTF8.GetString(ms.ToArray()));

            if (!healthy)
            {
                // Only report healthy once data flows: a bad API key connects
                // fine and is then closed, and must keep backing off.
                ReportHealthy();
                healthy = true;
            }
        }
    }

    protected override void CleanupConnection()
    {
        var ws = Interlocked.Exchange(ref _ws, null);
        ws?.Dispose();
    }

    private void ProcessMessage(string json)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<AisEnvelope>(json);
            if (envelope is null) return;

            MessageCount++;
            var meta = envelope.MetaData;

            store.Upsert(meta.MMSI, vessel =>
            {
                if (!string.IsNullOrWhiteSpace(meta.ShipName))
                    vessel = vessel with { Name = meta.ShipName.Trim() };

                vessel = vessel with
                {
                    Latitude = meta.Latitude,
                    Longitude = meta.Longitude,
                    LastUpdate = DateTimeOffset.UtcNow,
                    ProviderTimestamp = meta.TimeUtc,
                };

                return envelope.MessageType switch
                {
                    "PositionReport" when envelope.Message.PositionReport is { } pr => vessel with
                    {
                        Speed = pr.Sog,
                        Course = pr.Cog,
                        Heading = pr.TrueHeading,
                        NavStatus = pr.NavigationalStatus,
                    },

                    "ShipStaticData" when envelope.Message.ShipStaticData is { } sd => vessel with
                    {
                        Destination = sd.Destination?.Trim() ?? "",
                        CallSign = sd.CallSign?.Trim() ?? "",
                        ShipType = sd.Type,
                        Draught = sd.Draught,
                        Eta = sd.Eta,
                    },

                    "StandardClassBPositionReport" when envelope.Message.StandardClassBPositionReport is { } cb => vessel with
                    {
                        Speed = cb.Sog,
                        Course = cb.Cog,
                        Heading = cb.TrueHeading,
                    },

                    _ => vessel
                };
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ProcessMessage failed. JSON preview: {JsonPreview}",
                json[..Math.Min(json.Length, 200)]);
        }
    }
}

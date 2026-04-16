using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Serilog;

namespace ShipWatcher.NET;

public class AisClient : IAisDataSource
{
    private readonly string _apiKey;
    private readonly double[][][] _boundingBoxes;
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private static readonly ILogger Log = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.File(
            Path.Combine(AppContext.BaseDirectory, "shipwatcher.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
        .CreateLogger();

    public ConcurrentDictionary<long, Vessel> Vessels { get; } = new();
    public int MessageCount { get; private set; }
    public bool IsConnected => _ws?.State == WebSocketState.Open;
    public string? LastError { get; private set; }
    public string SourceName => "aisstream.io";

    public event Action? OnDataUpdated;

    public AisClient(string apiKey, double[][][] boundingBoxes)
    {
        _apiKey = apiKey;
        _boundingBoxes = boundingBoxes;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ws = new ClientWebSocket();

        try
        {
            Log.Information("Connecting to aisstream.io");
            await _ws.ConnectAsync(new Uri("wss://stream.aisstream.io/v0/stream"), _cts.Token);
            Log.Information("Connected. Sending subscription");

            var subscription = new AisSubscription(
                _apiKey,
                _boundingBoxes,
                ["PositionReport", "ShipStaticData", "StandardClassBPositionReport"]
            );

            var json = JsonSerializer.Serialize(subscription);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, _cts.Token);
            Log.Information("Subscription sent. Starting receive loop");

            _ = Task.Run(() => ReceiveLoop(_cts.Token), _cts.Token);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Log.Error(ex, "ConnectAsync failed");
        }
    }

    private async Task ReceiveLoop(CancellationToken ct)
    {
        var buffer = new byte[8192];

        while (!ct.IsCancellationRequested && _ws?.State == WebSocketState.Open)
        {
            try
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;

                do
                {
                    result = await _ws.ReceiveAsync(buffer, ct);
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                var text = Encoding.UTF8.GetString(ms.ToArray());
                ProcessMessage(text);
            }
            catch (OperationCanceledException)
            {
                Log.Debug("ReceiveLoop cancelled");
                break;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Log.Error(ex, "ReceiveLoop failed");
                break;
            }
        }
    }

    private void ProcessMessage(string json)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<AisEnvelope>(json);
            if (envelope is null) return;

            MessageCount++;
            var meta = envelope.MetaData;
            var vessel = Vessels.GetOrAdd(meta.MMSI, _ => new Vessel { MMSI = meta.MMSI });

            if (!string.IsNullOrWhiteSpace(meta.ShipName))
                vessel.Name = meta.ShipName.Trim();

            vessel.Latitude = meta.Latitude;
            vessel.Longitude = meta.Longitude;
            vessel.LastUpdate = meta.TimeUtc;

            switch (envelope.MessageType)
            {
                case "PositionReport" when envelope.Message.PositionReport is { } pr:
                    vessel.Speed = pr.Sog;
                    vessel.Course = pr.Cog;
                    vessel.Heading = pr.TrueHeading;
                    vessel.NavStatus = pr.NavigationalStatus;
                    break;

                case "ShipStaticData" when envelope.Message.ShipStaticData is { } sd:
                    vessel.Destination = sd.Destination?.Trim() ?? "";
                    vessel.CallSign = sd.CallSign?.Trim() ?? "";
                    break;

                case "StandardClassBPositionReport" when envelope.Message.StandardClassBPositionReport is { } cb:
                    vessel.Speed = cb.Sog;
                    vessel.Course = cb.Cog;
                    vessel.Heading = cb.TrueHeading;
                    break;
            }

            OnDataUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ProcessMessage failed. JSON preview: {JsonPreview}",
                json[..Math.Min(json.Length, 200)]);
        }
    }

    public void Disconnect()
    {
        try
        {
            _cts?.Cancel();
            _ws?.Dispose();
            _cts?.Dispose();
            _ws = null;
            _cts = null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Disconnect failed");
        }
    }

    public void Dispose()
    {
        Disconnect();
    }
}

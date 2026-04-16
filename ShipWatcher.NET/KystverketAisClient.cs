using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using Serilog;

namespace ShipWatcher.NET;

/// <summary>
/// AIS data source connecting to the Norwegian Coastal Administration (Kystverket)
/// raw TCP NMEA stream at 153.44.253.27:5631.
/// Provides open AIS data in IEC 62320-1 format (NMEA sentences).
/// </summary>
public class KystverketAisClient : IAisDataSource
{
    private const string Host = "153.44.253.27";
    private const int Port = 5631;

    private TcpClient? _tcp;
    private CancellationTokenSource? _cts;
    private readonly NmeaParser _parser = new();

    private static readonly ILogger Log = Serilog.Log.ForContext<KystverketAisClient>();

    public ConcurrentDictionary<long, Vessel> Vessels { get; } = new();
    public int MessageCount { get; private set; }
    public bool IsConnected => _tcp?.Connected == true;
    public string? LastError { get; private set; }
    public string SourceName => "Kystverket (Norway)";

    public event Action? OnDataUpdated;

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _tcp = new TcpClient();

        try
        {
            Log.Information("Connecting to Kystverket AIS at {Host}:{Port}", Host, Port);
            await _tcp.ConnectAsync(Host, Port, _cts.Token);
            Log.Information("Connected to Kystverket AIS stream");

            _ = Task.Run(() => ReceiveLoop(_cts.Token), _cts.Token);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Log.Error(ex, "Kystverket ConnectAsync failed");
        }
    }

    public void Disconnect()
    {
        try
        {
            _cts?.Cancel();
            _tcp?.Close();
            _tcp?.Dispose();
            _cts?.Dispose();
            _tcp = null;
            _cts = null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Kystverket Disconnect failed");
        }
    }

    private async Task ReceiveLoop(CancellationToken ct)
    {
        try
        {
            using var reader = new StreamReader(_tcp!.GetStream(), Encoding.ASCII);

            while (!ct.IsCancellationRequested && _tcp?.Connected == true)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line is null)
                    break;

                ProcessLine(line);
            }
        }
        catch (OperationCanceledException)
        {
            Log.Debug("Kystverket ReceiveLoop cancelled");
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Log.Error(ex, "Kystverket ReceiveLoop failed");
        }
    }

    private void ProcessLine(string line)
    {
        try
        {
            var result = _parser.Parse(line);
            if (result is null)
                return;

            MessageCount++;
            var vessel = Vessels.GetOrAdd(result.MMSI, _ => new Vessel { MMSI = result.MMSI });

            switch (result.MessageType)
            {
                case NmeaMessageType.PositionReport:
                    vessel.Latitude = result.Latitude;
                    vessel.Longitude = result.Longitude;
                    vessel.Speed = result.Sog;
                    vessel.Course = result.Cog;
                    vessel.Heading = result.TrueHeading;
                    vessel.NavStatus = result.NavigationalStatus;
                    vessel.LastUpdate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                    break;

                case NmeaMessageType.StaticData:
                    if (!string.IsNullOrWhiteSpace(result.Name))
                        vessel.Name = result.Name;
                    if (!string.IsNullOrWhiteSpace(result.CallSign))
                        vessel.CallSign = result.CallSign;
                    if (!string.IsNullOrWhiteSpace(result.Destination))
                        vessel.Destination = result.Destination;
                    vessel.LastUpdate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                    break;

                case NmeaMessageType.ClassBPositionReport:
                    vessel.Latitude = result.Latitude;
                    vessel.Longitude = result.Longitude;
                    vessel.Speed = result.Sog;
                    vessel.Course = result.Cog;
                    vessel.Heading = result.TrueHeading;
                    vessel.LastUpdate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                    break;
            }

            OnDataUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Kystverket ProcessLine failed: {Line}",
                line[..Math.Min(line.Length, 200)]);
        }
    }

    public void Dispose()
    {
        Disconnect();
    }
}

using System.Globalization;
using System.Net.Sockets;
using System.Text;
using Serilog;

namespace ShipWatcher.NET.Sources;

/// <summary>
/// AIS data source connecting to the Norwegian Coastal Administration (Kystverket)
/// raw TCP NMEA stream (default 153.44.253.27:5631, configurable in the source dialog).
/// Provides open AIS data in IEC 62320-1 format (NMEA sentences).
/// </summary>
public class KystverketAisClient(VesselStore store) : AisSourceBase, ISourceDescriptor
{
    private const string DefaultHost = "153.44.253.27";
    private const int DefaultPort = 5631;

    private string _host = DefaultHost;
    private int _port = DefaultPort;

    private TcpClient? _tcp;
    private readonly NmeaParser _parser = new();

    protected override ILogger Log { get; } = Serilog.Log.ForContext<KystverketAisClient>();

    public override bool IsConnected => _tcp?.Connected == true;
    public override string SourceName => "Kystverket (Norway)";

    // ISourceDescriptor
    public string DisplayLabel => "Kystverket (Norway, open data)";

    public IReadOnlyList<SourceConfigField> ConfigFields =>
    [
        new("host", "Host", _host),
        new("port", "Port", _port.ToString(CultureInfo.InvariantCulture)),
    ];

    public string? ValidateConfig()
    {
        if (string.IsNullOrWhiteSpace(_host))
            return "Host is required";
        if (_port is < 1 or > 65535)
            return "Port must be a number between 1 and 65535";
        return null;
    }

    public void ApplyConfig(IReadOnlyDictionary<string, string> values)
    {
        if (values.TryGetValue("host", out var host))
            _host = host.Trim();

        if (values.TryGetValue("port", out var portRaw))
        {
            _port = int.TryParse(portRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port)
                ? port
                : -1; // invalid marker so ValidateConfig rejects it
        }
    }

    protected override async Task ReceiveAsync(CancellationToken ct)
    {
        var tcp = new TcpClient();
        _tcp = tcp;

        Log.Information("Connecting to Kystverket AIS at {Host}:{Port}", _host, _port);
        await tcp.ConnectAsync(_host, _port, ct);
        Log.Information("Connected to Kystverket AIS stream");

        using var reader = new StreamReader(tcp.GetStream(), Encoding.ASCII);
        var healthy = false;

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null)
                return;

            ProcessLine(line);

            if (!healthy)
            {
                ReportHealthy();
                healthy = true;
            }
        }
    }

    protected override void CleanupConnection()
    {
        var tcp = Interlocked.Exchange(ref _tcp, null);
        tcp?.Close();
    }

    private void ProcessLine(string line)
    {
        try
        {
            var result = _parser.Parse(line);
            if (result is null)
                return;

            MessageCount++;
            var now = DateTimeOffset.UtcNow;
            store.Upsert(result.MMSI, vessel => result.MessageType switch
            {
                NmeaMessageType.PositionReport => vessel with
                {
                    Latitude = result.Latitude,
                    Longitude = result.Longitude,
                    Speed = result.Sog,
                    Course = result.Cog,
                    Heading = result.TrueHeading,
                    NavStatus = result.NavigationalStatus,
                    LastUpdate = now,
                },

                NmeaMessageType.StaticData => vessel with
                {
                    Name = string.IsNullOrWhiteSpace(result.Name) ? vessel.Name : result.Name,
                    CallSign = string.IsNullOrWhiteSpace(result.CallSign) ? vessel.CallSign : result.CallSign,
                    Destination = string.IsNullOrWhiteSpace(result.Destination) ? vessel.Destination : result.Destination,
                    ShipType = result.ShipType,
                    Draught = result.Draught,
                    Eta = new EtaInfo
                    {
                        Month = result.EtaMonth,
                        Day = result.EtaDay,
                        Hour = result.EtaHour,
                        Minute = result.EtaMinute,
                    },
                    LastUpdate = now,
                },

                NmeaMessageType.ClassBPositionReport => vessel with
                {
                    Latitude = result.Latitude,
                    Longitude = result.Longitude,
                    Speed = result.Sog,
                    Course = result.Cog,
                    Heading = result.TrueHeading,
                    LastUpdate = now,
                },

                _ => vessel
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Kystverket ProcessLine failed: {Line}",
                line[..Math.Min(line.Length, 200)]);
        }
    }
}

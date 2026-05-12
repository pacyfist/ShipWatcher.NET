using System.Text.Json.Serialization;

namespace ShipWatcher.NET.Sources;

// --- Locations (GeoJSON) ---

public record DigitrafficLocationCollection
{
    [JsonPropertyName("features")]
    public List<DigitrafficFeature> Features { get; init; } = [];
}

public record DigitrafficFeature
{
    [JsonPropertyName("mmsi")]
    public long Mmsi { get; init; }

    [JsonPropertyName("geometry")]
    public DigitrafficGeometry Geometry { get; init; } = new();

    [JsonPropertyName("properties")]
    public DigitrafficProperties Properties { get; init; } = new();
}

public record DigitrafficGeometry
{
    [JsonPropertyName("coordinates")]
    public double[] Coordinates { get; init; } = []; // [lon, lat]
}

public record DigitrafficProperties
{
    [JsonPropertyName("sog")]
    public double? Sog { get; init; }

    [JsonPropertyName("cog")]
    public double? Cog { get; init; }

    [JsonPropertyName("navStat")]
    public int? NavStat { get; init; }

    [JsonPropertyName("heading")]
    public int? Heading { get; init; }

    [JsonPropertyName("timestampExternal")]
    public long TimestampExternal { get; init; }
}

// --- Vessel Metadata ---

public record DigitrafficVessel
{
    [JsonPropertyName("mmsi")]
    public long Mmsi { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("callSign")]
    public string? CallSign { get; init; }

    [JsonPropertyName("destination")]
    public string? Destination { get; init; }

    [JsonPropertyName("shipType")]
    public int? ShipType { get; init; }
}

using System.Text.Json.Serialization;

namespace ShipWatcher.NET;

public record AisSubscription(
    [property: JsonPropertyName("APIKey")] string ApiKey,
    [property: JsonPropertyName("BoundingBoxes")] double[][][] BoundingBoxes,
    [property: JsonPropertyName("FilterMessageTypes")] string[] FilterMessageTypes
);

public record AisEnvelope
{
    [JsonPropertyName("MessageType")]
    public string MessageType { get; init; } = "";

    [JsonPropertyName("MetaData")]
    public AisMetaData MetaData { get; init; } = new();

    [JsonPropertyName("Message")]
    public AisMessageWrapper Message { get; init; } = new();
}

public record AisMetaData
{
    [JsonPropertyName("MMSI")]
    public long MMSI { get; init; }

    [JsonPropertyName("MMSI_String")]
    public long? MMSIString { get; init; }

    [JsonPropertyName("ShipName")]
    public string ShipName { get; init; } = "";

    [JsonPropertyName("latitude")]
    public double Latitude { get; init; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; init; }

    [JsonPropertyName("time_utc")]
    public string TimeUtc { get; init; } = "";
}

public record AisMessageWrapper
{
    [JsonPropertyName("PositionReport")]
    public PositionReport? PositionReport { get; init; }

    [JsonPropertyName("ShipStaticData")]
    public ShipStaticData? ShipStaticData { get; init; }

    [JsonPropertyName("StandardClassBPositionReport")]
    public StandardClassBPositionReport? StandardClassBPositionReport { get; init; }
}

public record PositionReport
{
    [JsonPropertyName("Sog")]
    public double Sog { get; init; }

    [JsonPropertyName("Cog")]
    public double Cog { get; init; }

    [JsonPropertyName("TrueHeading")]
    public int TrueHeading { get; init; }

    [JsonPropertyName("NavigationalStatus")]
    public int NavigationalStatus { get; init; }

    [JsonPropertyName("RateOfTurn")]
    public double RateOfTurn { get; init; }
}

public record ShipStaticData
{
    [JsonPropertyName("Destination")]
    public string Destination { get; init; } = "";

    [JsonPropertyName("CallSign")]
    public string CallSign { get; init; } = "";

    [JsonPropertyName("Type")]
    public int Type { get; init; }

    [JsonPropertyName("Draught")]
    public double Draught { get; init; }

    [JsonPropertyName("Eta")]
    public EtaInfo? Eta { get; init; }
}

public record EtaInfo
{
    [JsonPropertyName("Month")]
    public int Month { get; init; }

    [JsonPropertyName("Day")]
    public int Day { get; init; }

    [JsonPropertyName("Hour")]
    public int Hour { get; init; }

    [JsonPropertyName("Minute")]
    public int Minute { get; init; }
}

public record StandardClassBPositionReport
{
    [JsonPropertyName("Sog")]
    public double Sog { get; init; }

    [JsonPropertyName("Cog")]
    public double Cog { get; init; }

    [JsonPropertyName("TrueHeading")]
    public int TrueHeading { get; init; }
}

public class Vessel
{
    public long MMSI { get; set; }
    public string Name { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Speed { get; set; }
    public double Course { get; set; }
    public int Heading { get; set; }
    public string Destination { get; set; } = "";
    public string CallSign { get; set; } = "";
    public int NavStatus { get; set; }
    public string LastUpdate { get; set; } = "";

    public string NavStatusText => NavStatus switch
    {
        0 => "Under way",
        1 => "At anchor",
        2 => "Not commanded",
        3 => "Restricted",
        4 => "Constrained",
        5 => "Moored",
        6 => "Aground",
        7 => "Fishing",
        8 => "Sailing",
        14 => "AIS-SART",
        _ => "Unknown"
    };

    public string CoordinateString =>
        $"{Math.Abs(Latitude):F4}\u00b0{(Latitude >= 0 ? "N" : "S")} {Math.Abs(Longitude):F4}\u00b0{(Longitude >= 0 ? "E" : "W")}";
}

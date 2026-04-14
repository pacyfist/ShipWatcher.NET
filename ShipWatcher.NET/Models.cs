using System.Text.Json.Serialization;

namespace ShipWatcher.NET;

public record AisSubscription(
    [property: JsonPropertyName("APIKey")] string ApiKey,
    [property: JsonPropertyName("BoundingBoxes")] double[][][] BoundingBoxes,
    [property: JsonPropertyName("FilterMessageTypes")] string[] FilterMessageTypes
);

public class AisEnvelope
{
    [JsonPropertyName("MessageType")]
    public string MessageType { get; set; } = "";

    [JsonPropertyName("MetaData")]
    public AisMetaData MetaData { get; set; } = new();

    [JsonPropertyName("Message")]
    public AisMessageWrapper Message { get; set; } = new();
}

public class AisMetaData
{
    [JsonPropertyName("MMSI")]
    public long MMSI { get; set; }

    [JsonPropertyName("MMSI_String")]
    public long? MMSIString { get; set; }

    [JsonPropertyName("ShipName")]
    public string ShipName { get; set; } = "";

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("time_utc")]
    public string TimeUtc { get; set; } = "";
}

public class AisMessageWrapper
{
    [JsonPropertyName("PositionReport")]
    public PositionReport? PositionReport { get; set; }

    [JsonPropertyName("ShipStaticData")]
    public ShipStaticData? ShipStaticData { get; set; }

    [JsonPropertyName("StandardClassBPositionReport")]
    public StandardClassBPositionReport? StandardClassBPositionReport { get; set; }
}

public class PositionReport
{
    [JsonPropertyName("Sog")]
    public double Sog { get; set; }

    [JsonPropertyName("Cog")]
    public double Cog { get; set; }

    [JsonPropertyName("TrueHeading")]
    public int TrueHeading { get; set; }

    [JsonPropertyName("NavigationalStatus")]
    public int NavigationalStatus { get; set; }

    [JsonPropertyName("RateOfTurn")]
    public double RateOfTurn { get; set; }
}

public class ShipStaticData
{
    [JsonPropertyName("Destination")]
    public string Destination { get; set; } = "";

    [JsonPropertyName("CallSign")]
    public string CallSign { get; set; } = "";

    [JsonPropertyName("Type")]
    public int Type { get; set; }

    [JsonPropertyName("Draught")]
    public double Draught { get; set; }

    [JsonPropertyName("Eta")]
    public EtaInfo? Eta { get; set; }
}

public class EtaInfo
{
    [JsonPropertyName("Month")]
    public int Month { get; set; }

    [JsonPropertyName("Day")]
    public int Day { get; set; }

    [JsonPropertyName("Hour")]
    public int Hour { get; set; }

    [JsonPropertyName("Minute")]
    public int Minute { get; set; }
}

public class StandardClassBPositionReport
{
    [JsonPropertyName("Sog")]
    public double Sog { get; set; }

    [JsonPropertyName("Cog")]
    public double Cog { get; set; }

    [JsonPropertyName("TrueHeading")]
    public int TrueHeading { get; set; }
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

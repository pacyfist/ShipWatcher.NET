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

/// <summary>
/// Immutable snapshot of a vessel's state. Updates go through
/// <see cref="VesselStore.Upsert"/>, which swaps in a new instance atomically,
/// so readers never observe a torn lat/lon pair and no locking is needed.
/// </summary>
public record Vessel
{
    public required long MMSI { get; init; }
    public string Name { get; init; } = "";
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double Speed { get; init; }
    public double Course { get; init; }
    public int Heading { get; init; }
    public string Destination { get; init; } = "";
    public string CallSign { get; init; } = "";
    public int NavStatus { get; init; }
    public int ShipType { get; init; }
    public double Draught { get; init; }
    public EtaInfo? Eta { get; init; }

    /// <summary>When we last received any message for this vessel (local receipt time).</summary>
    public DateTimeOffset LastUpdate { get; init; }

    /// <summary>Raw timestamp string from the provider, if it supplies one.</summary>
    public string ProviderTimestamp { get; init; } = "";

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

    public string ShipTypeText => ShipType switch
    {
        0 => "N/A",
        30 => "Fishing",
        31 or 32 => "Towing",
        33 => "Dredging",
        34 => "Diving ops",
        35 => "Military",
        36 => "Sailing",
        37 => "Pleasure craft",
        >= 40 and <= 49 => "High-speed craft",
        50 => "Pilot",
        51 => "Search & rescue",
        52 => "Tug",
        53 => "Port tender",
        54 => "Anti-pollution",
        55 => "Law enforcement",
        58 => "Medical",
        >= 60 and <= 69 => "Passenger",
        >= 70 and <= 79 => "Cargo",
        >= 80 and <= 89 => "Tanker",
        >= 90 and <= 99 => "Other",
        _ => $"Type {ShipType}"
    };

    public string EtaText =>
        Eta is null || Eta.Month == 0
            ? "N/A"
            : $"{Eta.Month:D2}-{Eta.Day:D2} {Eta.Hour:D2}:{Eta.Minute:D2}";

    public string AgeText
    {
        get
        {
            if (LastUpdate == default)
                return "never";

            var age = DateTimeOffset.UtcNow - LastUpdate;
            if (age < TimeSpan.Zero)
                age = TimeSpan.Zero;

            return age.TotalSeconds < 60 ? $"{(int)age.TotalSeconds}s ago"
                 : age.TotalMinutes < 60 ? $"{(int)age.TotalMinutes}m ago"
                 : $"{(int)age.TotalHours}h ago";
        }
    }

    public string CoordinateString =>
        $"{Math.Abs(Latitude):F4}\u00b0{(Latitude >= 0 ? "N" : "S")} {Math.Abs(Longitude):F4}\u00b0{(Longitude >= 0 ? "E" : "W")}";
}
